using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Interfaces;
using ElectionForecaster.Core.Models;
using ElectionForecaster.Infrastructure.Data;
using ElectionForecaster.Infrastructure.Data.Entities;
using ElectionForecaster.Infrastructure.DataSources.Interfaces;
using ElectionForecaster.Infrastructure.DataSources.Models;
using ElectionForecaster.Infrastructure.DataSources.PredictionMarkets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private readonly DateTime _electionDate;

    public ForecastingOrchestrator(
        IEnumerable<IPredictionMarketSource> marketSources,
        IPollingSource pollingSource,
        IFundamentalsSource fundamentalsSource,
        IApprovalSource approvalSource,
        IRaceService raceService,
        WeightCalculator weightCalculator,
        MonteCarloSimulator simulator,
        ForecastDbContext dbContext,
        IConfiguration configuration,
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
        _electionDate = DateTime.Parse(configuration.GetValue<string>("Forecasting:ElectionDate") ?? "2026-11-03");
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

        // 3. Inject cross-cutting fundamentals inputs: the national mood (from approval) and
        //    incumbency direction (from the race's candidates). This is where the old model
        //    silently failed — incumbency was never set and the mood was hardcoded to zero.
        if (fundamentals != null)
        {
            fundamentals.NationalEnvironment = NationalEnvironmentFromApproval(approval);
            fundamentals.IncumbentIsDem = GetIncumbentIsDem(race);
        }

        // 4. Dynamic weights + time-varying uncertainty.
        var weights = _weightCalculator.CalculateWeights(marketOdds, polling, fundamentals, raceType);
        var daysToElection = (_electionDate - DateTime.UtcNow).TotalDays;
        var se = UncertaintyModel.MarginStandardError(daysToElection, raceType, polling?.PollCount ?? 0);

        // 5. Blend the signals in MARGIN space, then convert to probability once.
        var blendedMargin = BlendMargins(marketOdds, polling, fundamentals, weights, se);
        var demProb = Math.Clamp(ForecastMath.MarginToProbability(blendedMargin, se), 0.02, 0.98);
        var repProb = 1.0 - demProb;

        // 6. Vote shares follow directly from the blended margin.
        var demVoteShare = Math.Clamp(0.50 + blendedMargin / 200.0, 0.30, 0.70);

        // 7. Historical data (back to Nov 2025).
        var history = await GetForecastHistoryAsync(raceId, 365, cancellationToken);

        var forecast = new DetailedForecast
        {
            RaceId = raceId,
            DemWinProbability = demProb,
            RepWinProbability = repProb,
            DemVoteShare = demVoteShare,
            RepVoteShare = 1.0 - demVoteShare,
            ExpectedDemMargin = blendedMargin,
            MarginStdDev = se,
            Confidence = CalculateConfidence(marketOdds, polling, fundamentals),
            LastUpdated = DateTime.UtcNow,
            Inputs = new ForecastInputs
            {
                MarketOdds = marketOdds?.DemOdds,
                PollingAverage = polling?.DemPercent,
                PollingWinProbability = (polling != null && polling.PollCount > 0)
                    ? polling.GetDemWinProbability()
                    : null,
                FundamentalsPrediction = fundamentals != null
                    ? ForecastMath.MarginToProbability(fundamentals.GetExpectedDemMargin(), se)
                    : null,
                ApprovalAdjustment = approval - 50,
                MarketWeight = weights.MarketWeight,
                PollingWeight = weights.PollingWeight,
                FundamentalsWeight = weights.FundamentalsWeight,
                ApprovalWeight = 0,
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

    /// <summary>
    /// Blends the available signals into a single expected Dem margin (points). Each source is
    /// expressed as a margin — polls directly (Dem% − Rep%), fundamentals via their model, and
    /// the market probability inverted through the same SE so its implied margin round-trips back
    /// to its probability. Weighting happens here; the probability conversion happens once, later.
    /// </summary>
    private static double BlendMargins(
        MarketOdds? market,
        PollingAverage? polling,
        FundamentalsData? fundamentals,
        ForecastWeights weights,
        double se)
    {
        double totalWeight = 0, weightedMargin = 0;

        if (market != null && weights.MarketWeight > 0)
        {
            var marketMargin = ForecastMath.ProbabilityToMargin(market.DemOdds, se);
            weightedMargin += marketMargin * weights.MarketWeight;
            totalWeight += weights.MarketWeight;
        }

        if (polling != null && polling.PollCount > 0 && weights.PollingWeight > 0)
        {
            weightedMargin += polling.Margin * weights.PollingWeight;
            totalWeight += weights.PollingWeight;
        }

        if (fundamentals != null && weights.FundamentalsWeight > 0)
        {
            weightedMargin += fundamentals.GetExpectedDemMargin() * weights.FundamentalsWeight;
            totalWeight += weights.FundamentalsWeight;
        }

        return totalWeight > 0 ? weightedMargin / totalWeight : 0;
    }

    /// <summary>
    /// Projects the national environment (Dem margin, points) from presidential approval. In 2026
    /// the president is Republican, so Democrats are the midterm out-party. Low GOP approval →
    /// Dem-favorable environment. This already embeds the midterm effect — it is not added on top
    /// of a separate penalty. When a live generic-ballot average exists, prefer that instead.
    /// </summary>
    private static double NationalEnvironmentFromApproval(double approval)
    {
        const double approvalCoefficient = 0.5; // national-margin points per point of net approval
        const double midtermDrag = 2.5;          // baseline out-party bonus in a midterm
        var presidentNetApproval = 2 * approval - 100; // approval% → net (e.g. 43% → −14)
        var environment = -presidentNetApproval * approvalCoefficient + midtermDrag;
        return Math.Clamp(environment, -15, 15);
    }

    /// <summary>True if the incumbent is a Democrat, false if Republican, null for an open seat.</summary>
    private static bool? GetIncumbentIsDem(Race? race)
    {
        var incumbent = race?.Candidates.FirstOrDefault(c => c.IsIncumbent);
        return incumbent == null ? null : incumbent.Party == Party.Democrat;
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
