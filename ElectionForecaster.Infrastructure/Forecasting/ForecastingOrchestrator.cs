using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Interfaces;
using ElectionForecaster.Core.Models;
using ElectionForecaster.Infrastructure.Data;
using ElectionForecaster.Infrastructure.Data.Entities;
using ElectionForecaster.Infrastructure.DataSources.Interfaces;
using ElectionForecaster.Infrastructure.DataSources.Models;
using ElectionForecaster.Infrastructure.DataSources.PredictionMarkets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElectionForecaster.Infrastructure.Forecasting;

/// <summary>
/// Main forecasting engine that combines all data sources into unified predictions.
/// </summary>
public class ForecastingOrchestrator : IForecastingOrchestrator
{
    private readonly IEnumerable<IPredictionMarketSource> _marketSources;
    private readonly IPollingSource _pollingSource;
    private readonly IFundamentalsSource _fundamentalsSource;
    private readonly IApprovalSource _approvalSource;
    private readonly IRaceService _raceService;
    private readonly WeightCalculator _weightCalculator;
    private readonly MonteCarloSimulator _simulator;
    private readonly ForecastDbContext _dbContext;
    private readonly ILogger<ForecastingOrchestrator> _logger;

    public ForecastingOrchestrator(
        IEnumerable<IPredictionMarketSource> marketSources,
        IPollingSource pollingSource,
        IFundamentalsSource fundamentalsSource,
        IApprovalSource approvalSource,
        IRaceService raceService,
        WeightCalculator weightCalculator,
        MonteCarloSimulator simulator,
        ForecastDbContext dbContext,
        ILogger<ForecastingOrchestrator> logger)
    {
        _marketSources = marketSources;
        _pollingSource = pollingSource;
        _fundamentalsSource = fundamentalsSource;
        _approvalSource = approvalSource;
        _raceService = raceService;
        _weightCalculator = weightCalculator;
        _simulator = simulator;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<DetailedForecast> GenerateForecastAsync(string raceId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Generating forecast for {RaceId}", raceId);

        // 1. Get race info to determine race type
        var race = await _raceService.GetRaceByIdAsync(raceId);
        var raceType = race?.Type ?? RaceType.Senate;

        // 2. Collect all inputs in parallel
        var marketTask = GetAggregatedMarketOddsAsync(raceId, cancellationToken);
        var pollingTask = _pollingSource.GetPollingAverageAsync(raceId, cancellationToken);
        var fundamentalsTask = _fundamentalsSource.GetFundamentalsAsync(raceId, cancellationToken);
        var approvalTask = _approvalSource.GetPresidentialApprovalAsync(cancellationToken);

        await Task.WhenAll(marketTask, pollingTask, fundamentalsTask, approvalTask);

        var marketOdds = await marketTask;
        var polling = await pollingTask;
        var fundamentals = await fundamentalsTask;
        var approval = await approvalTask;

        // 3. Calculate dynamic weights
        var weights = _weightCalculator.CalculateWeights(
            marketOdds, polling, fundamentals, approval, raceType);

        // 4. Combine inputs into final probability
        var (demProb, repProb) = CombineInputs(marketOdds, polling, fundamentals, approval, weights);

        // 5. Calculate vote shares
        var (demVoteShare, repVoteShare) = EstimateVoteShares(polling, fundamentals);

        // 6. Get historical data (back to Nov 2025)
        var history = await GetForecastHistoryAsync(raceId, 365, cancellationToken);

        // 7. Build the detailed forecast
        var forecast = new DetailedForecast
        {
            RaceId = raceId,
            DemWinProbability = demProb,
            RepWinProbability = repProb,
            DemVoteShare = demVoteShare,
            RepVoteShare = repVoteShare,
            Confidence = CalculateConfidence(marketOdds, polling, fundamentals),
            LastUpdated = DateTime.UtcNow,
            Inputs = new ForecastInputs
            {
                MarketOdds = marketOdds?.DemOdds,
                PollingAverage = polling?.DemPercent,
                PollingWinProbability = (polling != null && polling.PollCount > 0)
                    ? polling.GetDemWinProbability()
                    : null,
                FundamentalsPrediction = fundamentals?.GetDemWinProbability(),
                ApprovalAdjustment = approval - 50,
                MarketWeight = weights.MarketWeight,
                PollingWeight = weights.PollingWeight,
                FundamentalsWeight = weights.FundamentalsWeight,
                ApprovalWeight = weights.ApprovalWeight,
                MarketLastUpdated = marketOdds?.Timestamp,
                PollingLastUpdated = polling?.LatestPollDate,
                PollCount = polling?.PollCount
            },
            History = history
        };

        return forecast;
    }

    public async Task<List<DetailedForecast>> GenerateAllForecastsAsync(RaceType? raceType = null, CancellationToken cancellationToken = default)
    {
        var races = await _raceService.GetAllRacesAsync(raceType);
        var forecasts = new List<DetailedForecast>();

        foreach (var race in races)
        {
            try
            {
                var forecast = await GenerateForecastAsync(race.Id, cancellationToken);
                forecasts.Add(forecast);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error generating forecast for {RaceId}", race.Id);
            }
        }

        return forecasts;
    }

    public async Task<ChamberForecast> SimulateChamberAsync(RaceType chamber, CancellationToken cancellationToken = default)
    {
        // Get all races for this chamber type
        var races = (await _raceService.GetAllRacesAsync(chamber)).ToList();

        // Generate forecasts for each race
        var forecasts = new List<DetailedForecast>();
        foreach (var race in races)
        {
            try
            {
                var forecast = await GenerateForecastAsync(race.Id, cancellationToken);
                forecasts.Add(forecast);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error forecasting {RaceId} for chamber simulation", race.Id);
            }
        }

        // Run Monte Carlo simulation
        var chamberResult = _simulator.SimulateChamber(forecasts, chamber);

        // Get historical data
        chamberResult.History = await GetChamberHistoryAsync(chamber.ToString(), 90, cancellationToken);

        return chamberResult;
    }

    public async Task RefreshAllDataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing all forecast data sources...");

        var tasks = new List<Task>();

        // Refresh market sources
        foreach (var source in _marketSources)
        {
            tasks.Add(source.RefreshAsync(cancellationToken));
        }

        // Refresh other sources
        tasks.Add(_pollingSource.RefreshAsync(cancellationToken));
        tasks.Add(_approvalSource.RefreshAsync(cancellationToken));

        await Task.WhenAll(tasks);

        _logger.LogInformation("All data sources refreshed");
    }

    public async Task StoreDailySnapshotAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;

        _logger.LogInformation("Storing daily forecast snapshot for {Date}", today);

        // Get all races
        var races = await _raceService.GetAllRacesAsync();

        foreach (var race in races)
        {
            try
            {
                // Check if we already have a snapshot for today
                var exists = await _dbContext.ForecastHistory.AnyAsync(
                    f => f.RaceId == race.Id && f.Date.Date == today,
                    cancellationToken);

                if (exists) continue;

                var forecast = await GenerateForecastAsync(race.Id, cancellationToken);

                var entity = new ForecastHistoryEntity
                {
                    RaceId = race.Id,
                    Date = today,
                    DemWinProbability = forecast.DemWinProbability,
                    RepWinProbability = forecast.RepWinProbability,
                    DemVoteShare = forecast.DemVoteShare,
                    RepVoteShare = forecast.RepVoteShare,
                    Confidence = forecast.Confidence,
                    MarketWeight = forecast.Inputs.MarketWeight,
                    PollingWeight = forecast.Inputs.PollingWeight,
                    FundamentalsWeight = forecast.Inputs.FundamentalsWeight,
                    ApprovalWeight = forecast.Inputs.ApprovalWeight,
                    MarketOdds = forecast.Inputs.MarketOdds,
                    PollingAverage = forecast.Inputs.PollingAverage,
                    FundamentalsPrediction = forecast.Inputs.FundamentalsPrediction,
                    ApprovalAdjustment = forecast.Inputs.ApprovalAdjustment
                };

                _dbContext.ForecastHistory.Add(entity);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error storing snapshot for {RaceId}", race.Id);
            }
        }

        // Also store chamber forecasts
        foreach (var chamber in new[] { RaceType.Senate, RaceType.House })
        {
            try
            {
                var exists = await _dbContext.ChamberHistory.AnyAsync(
                    c => c.Chamber == chamber.ToString() && c.Date.Date == today,
                    cancellationToken);

                if (exists) continue;

                var chamberForecast = await SimulateChamberAsync(chamber, cancellationToken);

                _dbContext.ChamberHistory.Add(new ChamberHistoryEntity
                {
                    Chamber = chamber.ToString(),
                    Date = today,
                    DemControlProbability = chamberForecast.DemControlProbability,
                    RepControlProbability = chamberForecast.RepControlProbability,
                    ExpectedDemSeats = chamberForecast.ExpectedDemSeats,
                    ExpectedRepSeats = chamberForecast.ExpectedRepSeats,
                    SimulationIterations = chamberForecast.SimulationIterations,
                    DemSeatsLow = chamberForecast.DemSeatRange.Low,
                    DemSeatsHigh = chamberForecast.DemSeatRange.High
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error storing chamber snapshot for {Chamber}", chamber);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Daily snapshot stored");
    }

    public async Task BackfillHistoryFromMarketsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting forecast history backfill from Polymarket...");

        // Find the PolymarketClient from the registered market sources
        var polymarketClient = _marketSources.OfType<PolymarketClient>().FirstOrDefault();
        if (polymarketClient == null)
        {
            _logger.LogWarning("PolymarketClient not found in registered market sources, cannot backfill");
            return;
        }

        await polymarketClient.BackfillHistoricalDataAsync(_dbContext, cancellationToken);
    }

    private async Task<MarketOdds?> GetAggregatedMarketOddsAsync(string raceId, CancellationToken cancellationToken)
    {
        var allOdds = new List<MarketOdds>();

        foreach (var source in _marketSources)
        {
            try
            {
                var odds = await source.GetRaceOddsAsync(raceId, cancellationToken);
                if (odds != null)
                {
                    allOdds.Add(odds);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error getting market odds from {Source}", source.SourceName);
            }
        }

        if (allOdds.Count == 0)
            return null;

        // Aggregate by volume-weighting
        double totalWeight = 0;
        double weightedDem = 0;
        double weightedRep = 0;
        double? maxVolume = null;

        foreach (var odds in allOdds)
        {
            var weight = odds.Volume ?? 1.0;
            totalWeight += weight;
            weightedDem += odds.DemOdds * weight;
            weightedRep += odds.RepOdds * weight;

            if (!maxVolume.HasValue || (odds.Volume ?? 0) > maxVolume)
                maxVolume = odds.Volume;
        }

        return new MarketOdds
        {
            RaceId = raceId,
            Source = "Aggregated",
            DemOdds = weightedDem / totalWeight,
            RepOdds = weightedRep / totalWeight,
            Timestamp = allOdds.Max(o => o.Timestamp),
            Volume = maxVolume
        };
    }

    private (double demProb, double repProb) CombineInputs(
        MarketOdds? market,
        PollingAverage? polling,
        FundamentalsData? fundamentals,
        double? approval,
        ForecastWeights weights)
    {
        double combinedDemProb = 0;

        // Market input
        if (market != null && weights.MarketWeight > 0)
        {
            combinedDemProb += market.DemOdds * weights.MarketWeight;
        }

        // Polling input
        if (polling != null && polling.PollCount > 0 && weights.PollingWeight > 0)
        {
            combinedDemProb += polling.GetDemWinProbability() * weights.PollingWeight;
        }

        // Fundamentals input
        if (fundamentals != null && weights.FundamentalsWeight > 0)
        {
            combinedDemProb += fundamentals.GetDemWinProbability() * weights.FundamentalsWeight;
        }

        // Approval adjustment
        if (approval.HasValue && weights.ApprovalWeight > 0)
        {
            // Approval affects the president's party
            // If president is Republican, low approval helps Democrats
            var approvalEffect = (approval.Value - 50) / 100.0; // -0.5 to +0.5
            // This shifts probability by a small amount based on approval
            // Assume Republican president for 2026
            var demAdjustment = -approvalEffect * 0.1; // Low approval = slight Dem boost
            combinedDemProb += demAdjustment * weights.ApprovalWeight;
        }

        // Ensure probabilities are valid
        combinedDemProb = Math.Max(0.01, Math.Min(0.99, combinedDemProb));
        var combinedRepProb = 1.0 - combinedDemProb;

        return (combinedDemProb, combinedRepProb);
    }

    private (double demShare, double repShare) EstimateVoteShares(
        PollingAverage? polling,
        FundamentalsData? fundamentals)
    {
        if (polling != null && polling.PollCount > 0)
        {
            // Normalize to two-party vote share
            var total = polling.DemPercent + polling.RepPercent;
            if (total > 0)
            {
                return (polling.DemPercent / total, polling.RepPercent / total);
            }
            return (polling.DemPercent / 100.0, polling.RepPercent / 100.0);
        }

        if (fundamentals != null)
        {
            var margin = fundamentals.GetExpectedDemMargin();
            // Convert margin to vote shares (assuming 50-50 baseline)
            var demShare = 0.50 + (margin / 100.0);
            demShare = Math.Max(0.30, Math.Min(0.70, demShare));
            return (demShare, 1.0 - demShare);
        }

        // Default to 50-50
        return (0.50, 0.50);
    }

    private double CalculateConfidence(
        MarketOdds? market,
        PollingAverage? polling,
        FundamentalsData? fundamentals)
    {
        double confidence = 0.5;
        int sources = 0;

        if (market != null)
        {
            confidence += market.Confidence * 0.2;
            sources++;
        }

        if (polling != null && polling.PollCount > 0)
        {
            confidence += polling.Confidence * 0.2;
            sources++;
        }

        if (fundamentals != null)
        {
            confidence += 0.15; // Fundamentals always available
            sources++;
        }

        // Bonus for having multiple sources
        if (sources >= 3)
            confidence += 0.1;
        else if (sources >= 2)
            confidence += 0.05;

        return Math.Min(0.95, confidence);
    }

    private async Task<List<HistoricalDataPoint>> GetForecastHistoryAsync(
        string raceId, int days, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);

        var history = await _dbContext.ForecastHistory
            .Where(f => f.RaceId == raceId && f.Date >= cutoff)
            .OrderBy(f => f.Date)
            .Select(f => new HistoricalDataPoint
            {
                Date = f.Date,
                DemWinProbability = f.DemWinProbability,
                RepWinProbability = f.RepWinProbability,
                DemVoteShare = f.DemVoteShare,
                RepVoteShare = f.RepVoteShare
            })
            .ToListAsync(cancellationToken);

        return history;
    }

    private async Task<List<ChamberHistoryPoint>> GetChamberHistoryAsync(
        string chamber, int days, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);

        var history = await _dbContext.ChamberHistory
            .Where(c => c.Chamber == chamber && c.Date >= cutoff)
            .OrderBy(c => c.Date)
            .Select(c => new ChamberHistoryPoint
            {
                Date = c.Date,
                DemControlProbability = c.DemControlProbability,
                ExpectedDemSeats = c.ExpectedDemSeats
            })
            .ToListAsync(cancellationToken);

        return history;
    }
}
