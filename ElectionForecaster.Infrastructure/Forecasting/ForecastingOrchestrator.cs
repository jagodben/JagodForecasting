using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Interfaces;
using ElectionForecaster.Core.Models;
using ElectionForecaster.Infrastructure.Data;
using ElectionForecaster.Infrastructure.Data.Entities;
using ElectionForecaster.Infrastructure.DataSources.Interfaces;
using ElectionForecaster.Infrastructure.DataSources.Models;
using ElectionForecaster.Infrastructure.DataSources.Polling;
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

        // 3. Historical data (back to Nov 2025), then blend.
        var history = await GetForecastHistoryAsync(raceId, 365, cancellationToken);
        return BuildForecast(raceId, raceType, marketOdds, polling, fundamentals, approval, race, DateTime.UtcNow, history);
    }

    /// <summary>
    /// Blends the inputs into a forecast as of <paramref name="asOf"/>. Shared by the live path and
    /// the retrospective backfill so they can never diverge — the only difference is the date, which
    /// drives the weights, the time-varying SE, and the timestamp.
    /// </summary>
    private DetailedForecast BuildForecast(
        string raceId, RaceType raceType,
        MarketOdds? marketOdds, PollingAverage? polling, FundamentalsData? fundamentals,
        double approval, Race? race, DateTime asOf, List<HistoricalDataPoint> history)
    {
        // Inject cross-cutting fundamentals inputs: national mood (from approval) and incumbency
        // direction (from the race's candidates).
        if (fundamentals != null)
        {
            fundamentals.NationalEnvironment = NationalEnvironmentFromApproval(approval);
            fundamentals.IncumbentIsDem = GetIncumbentIsDem(race);
        }

        var weights = _weightCalculator.CalculateWeights(marketOdds, polling, fundamentals, raceType, asOf);
        var daysToElection = (_electionDate - asOf).TotalDays;
        var se = UncertaintyModel.MarginStandardError(daysToElection, raceType, polling?.PollCount ?? 0);

        var blendedMargin = BlendMargins(marketOdds, polling, fundamentals, weights, se);
        var demProb = Math.Clamp(ForecastMath.MarginToProbability(blendedMargin, se), 0.02, 0.98);
        var demVoteShare = Math.Clamp(0.50 + blendedMargin / 200.0, 0.30, 0.70);

        return new DetailedForecast
        {
            RaceId = raceId,
            DemWinProbability = demProb,
            RepWinProbability = 1.0 - demProb,
            DemVoteShare = demVoteShare,
            RepVoteShare = 1.0 - demVoteShare,
            ExpectedDemMargin = blendedMargin,
            MarginStdDev = se,
            Confidence = CalculateConfidence(marketOdds, polling, fundamentals),
            LastUpdated = asOf,
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
    }

    private static ForecastHistoryEntity ToHistoryEntity(DetailedForecast f, DateTime date) => new()
    {
        RaceId = f.RaceId,
        Date = date,
        DemWinProbability = f.DemWinProbability,
        RepWinProbability = f.RepWinProbability,
        DemVoteShare = f.DemVoteShare,
        RepVoteShare = f.RepVoteShare,
        Confidence = f.Confidence,
        ExpectedDemMargin = f.ExpectedDemMargin,
        MarginStdDev = f.MarginStdDev,
        MarketWeight = f.Inputs.MarketWeight,
        PollingWeight = f.Inputs.PollingWeight,
        FundamentalsWeight = f.Inputs.FundamentalsWeight,
        ApprovalWeight = f.Inputs.ApprovalWeight,
        MarketOdds = f.Inputs.MarketOdds,
        PollingAverage = f.Inputs.PollingAverage,
        FundamentalsPrediction = f.Inputs.FundamentalsPrediction,
        ApprovalAdjustment = f.Inputs.ApprovalAdjustment
    };

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
                _dbContext.ForecastHistory.Add(ToHistoryEntity(forecast, today));
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

    public async Task BackfillModelHistoryAsync(CancellationToken cancellationToken = default)
    {
        var fromDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var today = DateTime.UtcNow.Date;
        _logger.LogInformation("Backfilling model history {From:d}..{To:d} for statewide races", fromDate, today);

        var polymarket = _marketSources.OfType<PolymarketClient>().FirstOrDefault();
        var approval = await _approvalSource.GetPresidentialApprovalAsync(cancellationToken);

        // Statewide races only (House has no markets and sparse polls; excluded per scope).
        var races = (await _raceService.GetAllRacesAsync())
            .Where(r => r.Type == RaceType.Senate || r.Type == RaceType.Governor)
            .ToList();

        int rowsAdded = 0;
        foreach (var race in races)
        {
            try
            {
                // Reconstruct the daily market series and load all stored polls (with conduct dates).
                var series = polymarket != null
                    ? await polymarket.GetDailyOddsSeriesAsync(race.Id, fromDate, cancellationToken)
                    : new List<(DateTime date, double demProb)>();
                var marketByDate = series
                    .GroupBy(s => s.date.Date)
                    .ToDictionary(g => g.Key, g => g.Last().demProb);
                var marketDays = marketByDate.Keys.OrderBy(d => d).ToList();

                var polls = await _pollingSource.GetRecentPollsAsync(race.Id, 3650, cancellationToken);
                var fundamentals = await _fundamentalsSource.GetFundamentalsAsync(race.Id, cancellationToken);

                // Rebuild the whole series from scratch (clears any stale market-only backfill rows).
                await _dbContext.ForecastHistory.Where(f => f.RaceId == race.Id).ExecuteDeleteAsync(cancellationToken);

                for (var day = fromDate.Date; day <= today; day = day.AddDays(1))
                {
                    // Market as of `day`: the exact daily point, else carry the most recent prior point.
                    double? demProb = marketByDate.TryGetValue(day, out var exact) ? exact : null;
                    if (demProb == null)
                    {
                        var prior = marketDays.LastOrDefault(d => d <= day);
                        if (prior != default) demProb = marketByDate[prior];
                    }
                    var market = demProb.HasValue
                        ? new MarketOdds { RaceId = race.Id, Source = "Polymarket", DemOdds = demProb.Value, RepOdds = 1 - demProb.Value, Timestamp = day }
                        : null;

                    // Polling as of `day`: polls conducted on or before that date, decayed to that date.
                    var pollsAsOf = polls.Where(p => p.Date.Date <= day).ToList();
                    var polling = pollsAsOf.Count > 0
                        ? PollingAverageCalculator.Calculate(pollsAsOf, race.Id, day)
                        : null;

                    var forecast = BuildForecast(race.Id, race.Type, market, polling, fundamentals, approval, race, day, new List<HistoricalDataPoint>());
                    _dbContext.ForecastHistory.Add(ToHistoryEntity(forecast, day));
                    rowsAdded++;
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error backfilling model history for {RaceId}", race.Id);
                _dbContext.ChangeTracker.Clear();
            }
        }

        _logger.LogInformation("Model history backfill complete: {Rows} day-rows across {Races} races", rowsAdded, races.Count);

        // Chamber control over time follows directly from the per-race history we just wrote.
        await BackfillChamberHistoryAsync(cancellationToken);
    }

    /// <summary>
    /// Rebuilds Senate control-over-time by running the Monte Carlo over each day's stored per-race
    /// forecasts. No re-fetch — the per-race margins/SEs already live in ForecastHistory. Senate only;
    /// House per-race history isn't backfilled (no district markets), so its chamber line isn't either.
    /// </summary>
    private async Task BackfillChamberHistoryAsync(CancellationToken cancellationToken)
    {
        const RaceType chamber = RaceType.Senate;
        var chamberName = chamber.ToString();

        var senateRaceIds = (await _raceService.GetAllRacesAsync(RaceType.Senate))
            .Select(r => r.Id).ToHashSet();

        var history = await _dbContext.ForecastHistory
            .Where(f => senateRaceIds.Contains(f.RaceId))
            .ToListAsync(cancellationToken);
        if (history.Count == 0) return;

        await _dbContext.ChamberHistory.Where(c => c.Chamber == chamberName).ExecuteDeleteAsync(cancellationToken);

        foreach (var day in history.GroupBy(f => f.Date.Date).OrderBy(g => g.Key))
        {
            var forecasts = day.Select(f => new DetailedForecast
            {
                RaceId = f.RaceId,
                DemWinProbability = f.DemWinProbability,
                ExpectedDemMargin = f.ExpectedDemMargin,
                MarginStdDev = f.MarginStdDev
            }).ToList();

            var result = _simulator.SimulateChamber(forecasts, chamber);
            _dbContext.ChamberHistory.Add(new ChamberHistoryEntity
            {
                Chamber = chamberName,
                Date = day.Key,
                DemControlProbability = result.DemControlProbability,
                RepControlProbability = result.RepControlProbability,
                ExpectedDemSeats = result.ExpectedDemSeats,
                ExpectedRepSeats = result.ExpectedRepSeats,
                SimulationIterations = result.SimulationIterations,
                DemSeatsLow = result.DemSeatRange.Low,
                DemSeatsHigh = result.DemSeatRange.High
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Chamber (Senate) history backfill complete");
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

    public async Task<List<ChamberHistoryPoint>> GetChamberHistoryAsync(
        string chamber, int days, CancellationToken cancellationToken = default)
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
