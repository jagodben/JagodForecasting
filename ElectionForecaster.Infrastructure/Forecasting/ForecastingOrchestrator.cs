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
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IGenericBallotSource _genericBallotSource;
    private readonly IRaceService _raceService;
    private readonly WeightCalculator _weightCalculator;
    private readonly MonteCarloSimulator _simulator;
    private readonly ForecastDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ForecastingOrchestrator> _logger;
    private readonly DateTime _electionDate;

    // Computed forecasts are cached for this long. External inputs refresh every ~15 min
    // (market) / 6 h (polling), so a few minutes of staleness is invisible but spares every
    // request the DB reads + blend math. Cache is a shared singleton across request scopes.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    // National environment (Dem margin, points) used only when no live generic-ballot average is
    // available — the baseline out-party bonus in a midterm (2026 has a Republican president).
    private const double DefaultMidtermEnvironment = 2.5;

    // Max races forecast concurrently when filling a cold cache — each runs in its own DI scope
    // (own DbContext) so this is real parallelism, bounded to avoid hammering the upstream APIs.
    private const int MaxForecastConcurrency = 8;

    private static string ForecastKey(string raceId) => $"forecast:{raceId}";
    private static string ChamberKey(RaceType chamber) => $"chamber:{chamber}";

    public ForecastingOrchestrator(
        IEnumerable<IPredictionMarketSource> marketSources,
        IPollingSource pollingSource,
        IFundamentalsSource fundamentalsSource,
        IGenericBallotSource genericBallotSource,
        IRaceService raceService,
        WeightCalculator weightCalculator,
        MonteCarloSimulator simulator,
        ForecastDbContext dbContext,
        IMemoryCache cache,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ForecastingOrchestrator> logger)
    {
        _marketSources = marketSources;
        _pollingSource = pollingSource;
        _fundamentalsSource = fundamentalsSource;
        _genericBallotSource = genericBallotSource;
        _raceService = raceService;
        _weightCalculator = weightCalculator;
        _simulator = simulator;
        _dbContext = dbContext;
        _cache = cache;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _electionDate = DateTime.Parse(configuration.GetValue<string>("Forecasting:ElectionDate") ?? "2026-11-03");
    }

    public async Task<DetailedForecast> GenerateForecastAsync(string raceId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(ForecastKey(raceId), out DetailedForecast? cached) && cached != null)
            return cached;

        var forecast = await BuildLiveForecastAsync(raceId, cancellationToken);
        _cache.Set(ForecastKey(raceId), forecast, CacheTtl);
        return forecast;
    }

    private async Task<DetailedForecast> BuildLiveForecastAsync(string raceId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Generating forecast for {RaceId}", raceId);

        // 1. Get race info to determine race type
        var race = await _raceService.GetRaceByIdAsync(raceId);
        var raceType = race?.Type ?? RaceType.Senate;

        // 2. Collect all inputs in parallel
        var marketTask = GetAggregatedMarketOddsAsync(raceId, cancellationToken);
        var pollingTask = _pollingSource.GetPollingAverageAsync(raceId, cancellationToken);
        var fundamentalsTask = _fundamentalsSource.GetFundamentalsAsync(raceId, cancellationToken);
        var genericBallotTask = _genericBallotSource.GetCurrentMarginAsync(cancellationToken);

        await Task.WhenAll(marketTask, pollingTask, fundamentalsTask, genericBallotTask);

        var marketOdds = await marketTask;
        var polling = await pollingTask;
        var fundamentals = await fundamentalsTask;
        var genericBallot = await genericBallotTask;

        // 3. Historical data (back to Nov 2025), then blend.
        var history = await GetForecastHistoryAsync(raceId, 365, cancellationToken);
        var forecast = BuildForecast(raceId, raceType, marketOdds, polling, fundamentals, genericBallot, race, DateTime.UtcNow, history);

        // The chart's most-recent point should equal the live headline. Stored history rows are
        // periodic snapshots (built from the daily market close) that can lag the current market;
        // replace/append today's point with this live forecast so the two always agree.
        var today = DateTime.UtcNow.Date;
        forecast.History = forecast.History
            .Where(h => h.Date.Date != today)
            .Append(new HistoricalDataPoint
            {
                Date = today,
                DemWinProbability = forecast.DemWinProbability,
                RepWinProbability = forecast.RepWinProbability,
                DemVoteShare = forecast.DemVoteShare,
                RepVoteShare = forecast.RepVoteShare
            })
            .OrderBy(h => h.Date)
            .ToList();

        return forecast;
    }

    /// <summary>
    /// Blends the inputs into a forecast as of <paramref name="asOf"/>. Shared by the live path and
    /// the retrospective backfill so they can never diverge — the only difference is the date, which
    /// drives the weights, the time-varying SE, and the timestamp.
    /// </summary>
    private DetailedForecast BuildForecast(
        string raceId, RaceType raceType,
        MarketOdds? marketOdds, PollingAverage? polling, FundamentalsData? fundamentals,
        double? genericBallotMargin, Race? race, DateTime asOf, List<HistoricalDataPoint> history)
    {
        // Inject cross-cutting fundamentals inputs: national mood and incumbency direction (from the
        // race's candidates). The national environment is the live generic-ballot average, falling
        // back to a fixed baseline midterm out-party bonus only when no average is available.
        if (fundamentals != null)
        {
            fundamentals.NationalEnvironment = genericBallotMargin.HasValue
                ? Math.Clamp(genericBallotMargin.Value, -15, 15)
                : DefaultMidtermEnvironment;
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
                    ? polling.GetDemWinProbability(se)
                    : null,
                FundamentalsPrediction = fundamentals != null
                    ? ForecastMath.MarginToProbability(fundamentals.GetExpectedDemMargin(), se)
                    : null,
                MarketWeight = weights.MarketWeight,
                PollingWeight = weights.PollingWeight,
                FundamentalsWeight = weights.FundamentalsWeight,
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
        MarketOdds = f.Inputs.MarketOdds,
        PollingAverage = f.Inputs.PollingAverage,
        FundamentalsPrediction = f.Inputs.FundamentalsPrediction
    };

    public async Task<List<DetailedForecast>> GenerateAllForecastsAsync(RaceType? raceType = null, CancellationToken cancellationToken = default)
    {
        var races = (await _raceService.GetAllRacesAsync(raceType)).ToList();
        var results = new DetailedForecast?[races.Count];
        var misses = new List<int>();

        // Serve every already-cached race straight from memory; only the misses need computing.
        for (int i = 0; i < races.Count; i++)
        {
            if (_cache.TryGetValue(ForecastKey(races[i].Id), out DetailedForecast? cached) && cached != null)
                results[i] = cached;
            else
                misses.Add(i);
        }

        // Compute the misses in parallel. Each runs in its own DI scope so it gets a private
        // DbContext (EF Core contexts aren't thread-safe) — sharing this instance's would throw.
        if (misses.Count > 0)
        {
            using var throttle = new SemaphoreSlim(MaxForecastConcurrency);
            await Task.WhenAll(misses.Select(async i =>
            {
                await throttle.WaitAsync(cancellationToken);
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var orchestrator = scope.ServiceProvider.GetRequiredService<IForecastingOrchestrator>();
                    results[i] = await orchestrator.GenerateForecastAsync(races[i].Id, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error generating forecast for {RaceId}", races[i].Id);
                }
                finally
                {
                    throttle.Release();
                }
            }));
        }

        return results.Where(f => f != null).Select(f => f!).ToList();
    }

    public async Task<ChamberForecast> SimulateChamberAsync(RaceType chamber, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(ChamberKey(chamber), out ChamberForecast? cached) && cached != null)
            return cached;

        // Per-race forecasts (cached + computed in parallel).
        var forecasts = await GenerateAllForecastsAsync(chamber, cancellationToken);

        // Every seat must be counted. A race whose forecast failed is dropped by the batch above,
        // which would shrink the seat total against the fixed control threshold (e.g. <435 House
        // seats vs the 218 majority line) and deflate BOTH parties' control odds. Backfill any gap
        // with the race's RaceService prior so the seat is still simulated with the right lean.
        var allRaces = (await _raceService.GetAllRacesAsync(chamber)).ToList();
        var byId = forecasts.ToDictionary(f => f.RaceId);
        var complete = allRaces
            .Select(r => byId.TryGetValue(r.Id, out var f) ? f : FallbackForecast(r))
            .ToList();
        var missing = allRaces.Count - byId.Count;
        if (missing > 0)
            _logger.LogWarning("{Chamber} sim: {Missing} race(s) missing a forecast — used the RaceService prior to keep the seat total complete", chamber, missing);

        var chamberResult = _simulator.SimulateChamber(complete, chamber);
        chamberResult.History = await GetChamberHistoryAsync(chamber.ToString(), 90, cancellationToken);

        _cache.Set(ChamberKey(chamber), chamberResult, CacheTtl);
        return chamberResult;
    }

    /// <summary>
    /// A minimal forecast reconstructed from a race's fundamentals-only RaceService prior. Used only
    /// to keep the chamber Monte Carlo's seat total complete when a race's full forecast is missing —
    /// never surfaced to the API.
    /// </summary>
    private static DetailedForecast FallbackForecast(Race race)
    {
        var demCand = race.Candidates.FirstOrDefault(c => c.Party == Party.Democrat);
        var repCand = race.Candidates.FirstOrDefault(c => c.Party == Party.Republican);
        var demForecast = race.Forecasts.FirstOrDefault(f => f.CandidateId == demCand?.Id);
        var repForecast = race.Forecasts.FirstOrDefault(f => f.CandidateId == repCand?.Id);
        var demVoteShare = demForecast?.ProjectedVoteShare ?? 0.5;
        return new DetailedForecast
        {
            RaceId = race.Id,
            DemWinProbability = demForecast?.WinProbability ?? 0.5,
            RepWinProbability = repForecast?.WinProbability ?? 0.5,
            // RaceService sets demVoteShare = 0.5 + margin/100, so invert to recover its margin.
            ExpectedDemMargin = (demVoteShare - 0.5) * 100.0,
            MarginStdDev = race.Type == RaceType.House ? 8.0 : 6.0
        };
    }

    /// <summary>
    /// Refreshes only the prediction-market sources. Markets move often, so this runs on a short
    /// cadence — kept separate from polling so we don't re-hit Wikipedia every market cycle.
    /// </summary>
    public async Task RefreshMarketDataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing prediction-market data...");
        await Task.WhenAll(_marketSources.Select(s => s.RefreshAsync(cancellationToken)));
        await ClearForecastCacheAsync();
    }

    /// <summary>
    /// Refreshes the polling and generic-ballot sources. Polls update slowly and the Wikipedia
    /// fetch is expensive/rate-limited, so this runs on a long cadence.
    /// </summary>
    public async Task RefreshPollingDataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing polling and generic-ballot data...");
        await Task.WhenAll(
            _pollingSource.RefreshAsync(cancellationToken),
            _genericBallotSource.RefreshAsync(cancellationToken));
        await ClearForecastCacheAsync();
    }

    public async Task RefreshAllDataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing all forecast data sources...");

        var tasks = _marketSources.Select(s => s.RefreshAsync(cancellationToken)).ToList();
        tasks.Add(_pollingSource.RefreshAsync(cancellationToken));
        tasks.Add(_genericBallotSource.RefreshAsync(cancellationToken));
        await Task.WhenAll(tasks);

        await ClearForecastCacheAsync();
        _logger.LogInformation("All data sources refreshed");
    }

    /// <summary>
    /// Drops cached forecasts so freshly-pulled inputs take effect immediately rather than waiting
    /// out the TTL. Keys are per-race plus the two chambers.
    /// </summary>
    private async Task ClearForecastCacheAsync()
    {
        var races = await _raceService.GetAllRacesAsync();
        foreach (var race in races)
            _cache.Remove(ForecastKey(race.Id));
        _cache.Remove(ChamberKey(RaceType.Senate));
        _cache.Remove(ChamberKey(RaceType.House));
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

    public async Task BackfillModelHistoryAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        // This deletes and rebuilds every statewide race's history from *today's* inputs, so re-running
        // it on each restart would wipe the genuine daily snapshots (StoreDailySnapshotAsync) and
        // retro-stamp today's national mood across all past days. Only seed when there's no history
        // yet; the manual admin endpoint passes force to rebuild deliberately.
        if (!force && await _dbContext.ForecastHistory.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Model history already present — skipping automatic backfill (POST /api/forecast/backfill to force a rebuild)");
            return;
        }

        var fromDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var today = DateTime.UtcNow.Date;
        _logger.LogInformation("Backfilling model history {From:d}..{To:d} for statewide races", fromDate, today);

        var polymarket = _marketSources.OfType<PolymarketClient>().FirstOrDefault();
        // Current generic-ballot margin applied across the backfilled days (a single national-mood
        // value for this retrospective reconstruction).
        var genericBallot = await _genericBallotSource.GetCurrentMarginAsync(cancellationToken);

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

                    var forecast = BuildForecast(race.Id, race.Type, market, polling, fundamentals, genericBallot, race, day, new List<HistoricalDataPoint>());
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
            // Two-party (undecideds split proportionally) so the poll is on the same final-result
            // scale as the market and fundamentals margins.
            weightedMargin += polling.TwoPartyMargin * weights.PollingWeight;
            totalWeight += weights.PollingWeight;
        }

        if (fundamentals != null && weights.FundamentalsWeight > 0)
        {
            weightedMargin += fundamentals.GetExpectedDemMargin() * weights.FundamentalsWeight;
            totalWeight += weights.FundamentalsWeight;
        }

        return totalWeight > 0 ? weightedMargin / totalWeight : 0;
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
