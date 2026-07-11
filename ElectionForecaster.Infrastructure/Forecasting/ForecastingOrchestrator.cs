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

    // The site serves a daily-frozen forecast: the map and charts reflect the snapshot taken at
    // 8 AM Eastern that morning and don't move until the next one. Serving from the persisted
    // snapshot (not a live recompute) is what makes it survive the free-tier server restarts.
    private static readonly TimeZoneInfo EasternZone = ResolveEastern();
    private const int SnapshotHourEastern = 8; // 8 AM Eastern

    // Charts (and the retrospective history behind them) begin here; earlier snapshots exist in
    // the DB but aren't shown. The served headline still uses the most recent snapshot regardless.
    private static readonly DateTime ChartStartDate = new(2026, 7, 1);

    // Served forecasts change once a day, so cache them until the next 8 AM Eastern rather than a
    // short TTL; the snapshot job also clears the cache when it writes, so the new day shows at once.
    // Floored to a minute so a cache-fill right at the 8 AM boundary never gets a non-positive TTL.
    private static TimeSpan CacheTtl
    {
        get
        {
            var ttl = NextSnapshotUtc() - DateTime.UtcNow;
            return ttl > TimeSpan.FromMinutes(1) ? ttl : TimeSpan.FromMinutes(1);
        }
    }

    // National environment (Dem margin, points) used only when no live generic-ballot average is
    // available — the baseline out-party bonus in a midterm (2026 has a Republican president).
    private const double DefaultMidtermEnvironment = 2.5;

    // Max races forecast concurrently when filling a cold cache — each runs in its own DI scope
    // (own DbContext) so this is real parallelism, bounded to avoid hammering the upstream APIs.
    private const int MaxForecastConcurrency = 8;

    private static TimeZoneInfo ResolveEastern()
    {
        foreach (var id in new[] { "America/New_York", "Eastern Standard Time" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
        return TimeZoneInfo.Utc;
    }

    /// <summary>The current calendar date in the Eastern time zone (the "forecast day").</summary>
    public static DateTime EasternToday()
        => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EasternZone).Date;

    /// <summary>UTC instant of the next 8 AM Eastern (today's if still upcoming, else tomorrow's).</summary>
    private static DateTime NextSnapshotUtc()
    {
        var nowEastern = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EasternZone);
        var next = nowEastern.Date.AddHours(SnapshotHourEastern);
        if (nowEastern >= next) next = next.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(next, DateTimeKind.Unspecified), EasternZone);
    }

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

        // Serve the most recent stored daily snapshot (taken at 8 AM Eastern) so the forecast is
        // frozen for the day and identical across server restarts. Falls back to a live compute
        // only before the first snapshot exists (fresh DB).
        var history = await GetForecastHistoryAsync(raceId, cancellationToken);
        var latest = await _dbContext.ForecastHistory
            .Where(f => f.RaceId == raceId)
            .OrderByDescending(f => f.Date)
            .FirstOrDefaultAsync(cancellationToken);

        var forecast = latest != null
            ? EntityToForecast(latest, history)
            : await ComputeForecastAsync(raceId, history, cancellationToken);

        _cache.Set(ForecastKey(raceId), forecast, CacheTtl);
        return forecast;
    }

    /// <summary>
    /// Computes a fresh forecast from the current live inputs. Used to produce the daily snapshot
    /// and as the first-run fallback — never the everyday serving path (that reads the snapshot).
    /// </summary>
    private async Task<DetailedForecast> ComputeForecastAsync(string raceId, List<HistoricalDataPoint> history, CancellationToken cancellationToken)
    {
        var race = await _raceService.GetRaceByIdAsync(raceId);
        var raceType = race?.Type ?? RaceType.Senate;

        var marketTask = GetAggregatedMarketOddsAsync(raceId, cancellationToken);
        var pollingTask = _pollingSource.GetPollingAverageAsync(raceId, cancellationToken);
        var fundamentalsTask = _fundamentalsSource.GetFundamentalsAsync(raceId, cancellationToken);
        var genericBallotTask = _genericBallotSource.GetCurrentMarginAsync(cancellationToken);

        await Task.WhenAll(marketTask, pollingTask, fundamentalsTask, genericBallotTask);

        return BuildForecast(raceId, raceType, await marketTask, await pollingTask,
            await fundamentalsTask, await genericBallotTask, race, DateTime.UtcNow, history);
    }

    /// <summary>Reconstructs the served forecast from a stored daily-snapshot row.</summary>
    private static DetailedForecast EntityToForecast(ForecastHistoryEntity e, List<HistoricalDataPoint> history) => new()
    {
        RaceId = e.RaceId,
        DemWinProbability = e.DemWinProbability,
        RepWinProbability = e.RepWinProbability,
        DemVoteShare = e.DemVoteShare,
        RepVoteShare = e.RepVoteShare,
        ExpectedDemMargin = e.ExpectedDemMargin,
        MarginStdDev = e.MarginStdDev,
        Confidence = e.Confidence,
        LastUpdated = e.Date,
        Inputs = new ForecastInputs
        {
            MarketOdds = e.MarketOdds,
            PollingAverage = e.PollingAverage,
            FundamentalsPrediction = e.FundamentalsPrediction,
            MarketWeight = e.MarketWeight,
            PollingWeight = e.PollingWeight,
            FundamentalsWeight = e.FundamentalsWeight,
        },
        History = history
    };

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
        // A viable independent occupies the challenger slot for this race; the parsed D-vs-R polls
        // price the token Democrat, not the independent, so they'd badly understate the challenger.
        // Drop polling here and lean on the market (mapped to price the independent) + fundamentals.
        if (IndependentChallengers.Has(raceId)) polling = null;

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

        // Safety net: if a liquid market sharply disagrees with the structural blend, lean on the
        // market — it usually knows something PVI/priors/polls-so-far can't (the VT-GOV class).
        ApplyMarketDisagreementGuard(weights, marketOdds, polling, fundamentals, se, raceId);

        // Loose display clamp only — the t-distribution's fat tails already keep blowouts
        // under ~99.9%, and a tighter clamp would skew seat totals vs the chamber sim.
        var blendedMargin = BlendMargins(marketOdds, polling, fundamentals, weights, se);
        var demProb = Math.Clamp(ForecastMath.MarginToProbability(blendedMargin, se), 0.005, 0.995);
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

        var history = await GetChamberHistoryAsync(chamber.ToString(), 0, cancellationToken);

        // Serve the most recent stored chamber snapshot so control odds are frozen for the day and
        // stable across restarts; only run the Monte Carlo live before the first snapshot exists.
        var latest = await _dbContext.ChamberHistory
            .Where(c => c.Chamber == chamber.ToString())
            .OrderByDescending(c => c.Date)
            .FirstOrDefaultAsync(cancellationToken);

        var chamberResult = latest != null
            ? EntityToChamber(latest, chamber, history)
            : await SimulateChamberLiveAsync(chamber, history, cancellationToken);

        _cache.Set(ChamberKey(chamber), chamberResult, CacheTtl);
        return chamberResult;
    }

    /// <summary>Reconstructs the served chamber forecast from a stored daily-snapshot row.</summary>
    private static ChamberForecast EntityToChamber(ChamberHistoryEntity e, RaceType chamber, List<ChamberHistoryPoint> history) => new()
    {
        Chamber = chamber.ToString(),
        DemControlProbability = e.DemControlProbability,
        RepControlProbability = e.RepControlProbability,
        ExpectedDemSeats = e.ExpectedDemSeats,
        ExpectedRepSeats = e.ExpectedRepSeats,
        SeatsNeededForControl = chamber == RaceType.Senate ? 51 : 218,
        SimulationIterations = e.SimulationIterations,
        DemSeatRange = new SeatRange { Low = e.DemSeatsLow, High = e.DemSeatsHigh, Median = (int)Math.Round(e.ExpectedDemSeats) },
        LastUpdated = e.Date,
        History = history
    };

    /// <summary>
    /// Runs the chamber Monte Carlo over the current per-race forecasts. Used to produce the daily
    /// snapshot and as the first-run fallback — not the everyday serving path.
    /// </summary>
    private async Task<ChamberForecast> SimulateChamberLiveAsync(RaceType chamber, List<ChamberHistoryPoint> history, CancellationToken cancellationToken)
    {
        var forecasts = await GenerateAllForecastsAsync(chamber, cancellationToken);

        // Every seat must be counted: a dropped race would shrink the seat total against the
        // fixed control threshold and deflate both parties' odds. Fill gaps from the baseline.
        var allRaces = (await _raceService.GetAllRacesAsync(chamber)).ToList();
        var byId = forecasts.ToDictionary(f => f.RaceId);
        var complete = allRaces
            .Select(r => byId.TryGetValue(r.Id, out var f) ? f : FallbackForecast(r))
            .ToList();

        var chamberResult = _simulator.SimulateChamber(complete, chamber);
        chamberResult.History = history;
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

    /// <summary>True if today's (Eastern) daily snapshot has already been stored.</summary>
    public async Task<bool> HasDailySnapshotAsync(CancellationToken cancellationToken = default)
    {
        var today = EasternToday();
        return await _dbContext.ForecastHistory.AnyAsync(f => f.Date == today, cancellationToken);
    }

    /// <summary>
    /// The once-a-day update: refresh every data source, then store the 8 AM snapshot that the site
    /// serves all day. No-ops (returns false) if today's snapshot already exists, so it runs exactly
    /// once per Eastern day even across the free-tier's frequent restarts.
    /// </summary>
    public async Task<bool> RunDailyUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (await HasDailySnapshotAsync(cancellationToken))
            return false;

        await RefreshAllDataAsync(cancellationToken);
        await StoreDailySnapshotAsync(cancellationToken);
        return true;
    }

    public async Task StoreDailySnapshotAsync(CancellationToken cancellationToken = default)
    {
        var today = EasternToday();
        _logger.LogInformation("Storing daily forecast snapshot for {Date:d} (Eastern)", today);

        // Per-race snapshots. Compute live for any race missing today's row (idempotent: an already
        // stored row is never recomputed, so the day stays frozen even if this resumes after a crash).
        var races = (await _raceService.GetAllRacesAsync()).ToList();
        foreach (var race in races)
        {
            try
            {
                if (await _dbContext.ForecastHistory.AnyAsync(f => f.RaceId == race.Id && f.Date == today, cancellationToken))
                    continue;

                var forecast = await ComputeForecastAsync(race.Id, new List<HistoricalDataPoint>(), cancellationToken);
                _dbContext.ForecastHistory.Add(ToHistoryEntity(forecast, today));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error storing snapshot for {RaceId}", race.Id);
            }
        }
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Chamber snapshots run the Monte Carlo over today's stored per-race rows, so the chamber
        // and per-race snapshots always describe the same frozen forecast.
        foreach (var chamber in new[] { RaceType.Senate, RaceType.House })
        {
            try
            {
                if (await _dbContext.ChamberHistory.AnyAsync(c => c.Chamber == chamber.ToString() && c.Date == today, cancellationToken))
                    continue;

                var chamberRaceIds = (await _raceService.GetAllRacesAsync(chamber)).Select(r => r.Id).ToHashSet();
                var todaysRows = await _dbContext.ForecastHistory
                    .Where(f => f.Date == today && chamberRaceIds.Contains(f.RaceId))
                    .ToListAsync(cancellationToken);
                var forecasts = todaysRows.Select(r => new DetailedForecast
                {
                    RaceId = r.RaceId,
                    ExpectedDemMargin = r.ExpectedDemMargin,
                    MarginStdDev = r.MarginStdDev
                }).ToList();
                if (forecasts.Count == 0) continue;

                var result = _simulator.SimulateChamber(forecasts, chamber);
                _dbContext.ChamberHistory.Add(new ChamberHistoryEntity
                {
                    Chamber = chamber.ToString(),
                    Date = today,
                    DemControlProbability = result.DemControlProbability,
                    RepControlProbability = result.RepControlProbability,
                    ExpectedDemSeats = result.ExpectedDemSeats,
                    ExpectedRepSeats = result.ExpectedRepSeats,
                    SimulationIterations = result.SimulationIterations,
                    DemSeatsLow = result.DemSeatRange.Low,
                    DemSeatsHigh = result.DemSeatRange.High
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error storing chamber snapshot for {Chamber}", chamber);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await ClearForecastCacheAsync();
        _logger.LogInformation("Daily snapshot stored");
    }

    public async Task BackfillModelHistoryAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        // Rebuilding wipes the genuine daily snapshots, so only seed automatically when history
        // is empty; the admin endpoint passes force to rebuild deliberately.
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
        await BackfillHouseChamberHistoryAsync(cancellationToken);
    }

    /// <summary>
    /// Rebuilds Senate control-over-time by running the Monte Carlo over each day's stored per-race
    /// forecasts. No re-fetch — the per-race margins/SEs already live in ForecastHistory. (The House
    /// line is rebuilt separately from the generic-ballot series; see BackfillHouseChamberHistoryAsync.)
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

    /// <summary>
    /// Rebuilds House control-over-time from the daily generic-ballot series — the House model's
    /// only time-varying input. Replays the current model, so a rebuild also purges rows written
    /// by older code; the daily snapshot extends the line going forward.
    /// </summary>
    private async Task BackfillHouseChamberHistoryAsync(CancellationToken cancellationToken)
    {
        var ballotDays = await _dbContext.GenericBallot
            .OrderBy(g => g.Date)
            .ToListAsync(cancellationToken);
        if (ballotDays.Count == 0) return;

        var houseRaces = (await _raceService.GetAllRacesAsync(RaceType.House)).ToList();

        // Per-race structural fundamentals (PVI, prior result, incumbency) are static — fetch once;
        // only the national environment and the time-to-election SE vary by day.
        var fundamentalsById = new Dictionary<string, FundamentalsData>();
        var incumbentById = new Dictionary<string, bool?>();
        foreach (var race in houseRaces)
        {
            fundamentalsById[race.Id] = await _fundamentalsSource.GetFundamentalsAsync(race.Id, cancellationToken);
            incumbentById[race.Id] = GetIncumbentIsDem(race);
        }

        var chamberName = RaceType.House.ToString();
        await _dbContext.ChamberHistory.Where(c => c.Chamber == chamberName).ExecuteDeleteAsync(cancellationToken);

        foreach (var day in ballotDays.GroupBy(g => g.Date.Date).OrderBy(g => g.Key))
        {
            var ballot = day.Last();
            var environment = Math.Clamp(ballot.DemPercent - ballot.RepPercent, -15, 15);
            var daysToElection = (_electionDate - day.Key).TotalDays;
            var se = UncertaintyModel.MarginStandardError(daysToElection, RaceType.House, 0);

            var forecasts = houseRaces.Select(race =>
            {
                var f = fundamentalsById[race.Id];
                f.NationalEnvironment = environment;
                f.IncumbentIsDem = incumbentById[race.Id];
                return new DetailedForecast
                {
                    RaceId = race.Id,
                    ExpectedDemMargin = f.GetExpectedDemMargin(),
                    MarginStdDev = se
                };
            }).ToList();

            var result = _simulator.SimulateChamber(forecasts, RaceType.House);
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
        _logger.LogInformation("Chamber (House) history backfill complete: {Days} days", ballotDays.GroupBy(g => g.Date.Date).Count());
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
    /// Blends the signals into one expected Dem margin. Every source is expressed as a margin
    /// (the market probability is inverted through the same SE used later, so it round-trips);
    /// the margin-to-probability conversion happens once, downstream.
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

    // --- Market-disagreement guard ---
    // A liquid market that diverges sharply from the poll+fundamentals blend usually knows
    // something the structural model can't see, so its weight ramps up with the gap — but
    // never to full dominance, and only above a liquidity floor.
    private const double LiquidMarketConfidence = 0.5;  // below this the market is too thin to override
    private const double DisagreementThreshold = 10.0;  // margin points of gap before the guard engages
    private const double DisagreementSpan = 20.0;       // further points over which it ramps to the cap
    private const double MaxGuardedMarketWeight = 0.70; // ceiling on the market's post-guard share

    /// <summary>
    /// Boosts the market weight (in place) when a liquid market's implied margin disagrees sharply
    /// with the poll+fundamentals blend. No-op for thin markets or small disagreements.
    /// </summary>
    private void ApplyMarketDisagreementGuard(
        ForecastWeights weights, MarketOdds? market, PollingAverage? polling,
        FundamentalsData? fundamentals, double se, string raceId)
    {
        if (market == null || weights.MarketWeight <= 0 || market.Confidence < LiquidMarketConfidence)
            return;

        double structuralWeight = weights.PollingWeight + weights.FundamentalsWeight;
        if (structuralWeight <= 0) return; // market is already the only signal

        double structuralMargin = 0;
        if (polling != null && polling.PollCount > 0)
            structuralMargin += polling.TwoPartyMargin * weights.PollingWeight;
        if (fundamentals != null)
            structuralMargin += fundamentals.GetExpectedDemMargin() * weights.FundamentalsWeight;
        structuralMargin /= structuralWeight;

        double marketMargin = ForecastMath.ProbabilityToMargin(market.DemOdds, se);
        double disagreement = Math.Abs(marketMargin - structuralMargin);
        if (disagreement <= DisagreementThreshold) return;

        double t = Math.Clamp((disagreement - DisagreementThreshold) / DisagreementSpan, 0, 1);
        double targetMarketWeight = weights.MarketWeight + t * (MaxGuardedMarketWeight - weights.MarketWeight);
        if (targetMarketWeight <= weights.MarketWeight) return;

        // Rescale polling+fundamentals to fill the remainder, preserving their relative split.
        double scale = (1 - targetMarketWeight) / structuralWeight;
        weights.MarketWeight = targetMarketWeight;
        weights.PollingWeight *= scale;
        weights.FundamentalsWeight *= scale;

        _logger.LogInformation(
            "Market-disagreement guard on {RaceId}: market {Market:+0.0;-0.0} vs structural {Structural:+0.0;-0.0} ({Gap:0.0} pts) -> market weight {Weight:0.00}",
            raceId, marketMargin, structuralMargin, disagreement, targetMarketWeight);
    }

    /// <summary>
    /// True if the incumbent is a Democrat, false if Republican, null for an open seat.
    /// Candidate flags first; when no candidate carries the flag (placeholder nominees before a
    /// primary concludes), fall back to the curated <see cref="StatewideIncumbents"/> table so an
    /// incumbent who IS running (e.g. VT's Phil Scott pre-primary) isn't miscounted as an open
    /// seat — that would drop their prior result from the fundamentals.
    /// </summary>
    private static bool? GetIncumbentIsDem(Race? race)
    {
        var incumbent = race?.Candidates.FirstOrDefault(c => c.IsIncumbent);
        if (incumbent != null) return incumbent.Party == Party.Democrat;
        return race != null ? StatewideIncumbents.GetIncumbentIsDem(race.Id) : null;
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
        string raceId, CancellationToken cancellationToken)
    {
        return await _dbContext.ForecastHistory
            .Where(f => f.RaceId == raceId && f.Date >= ChartStartDate)
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
    }

    // The `days` parameter is kept for the interface but the chart floor (July 1) always wins.
    public async Task<List<ChamberHistoryPoint>> GetChamberHistoryAsync(
        string chamber, int days, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ChamberHistory
            .Where(c => c.Chamber == chamber && c.Date >= ChartStartDate)
            .OrderBy(c => c.Date)
            .Select(c => new ChamberHistoryPoint
            {
                Date = c.Date,
                DemControlProbability = c.DemControlProbability,
                ExpectedDemSeats = c.ExpectedDemSeats
            })
            .ToListAsync(cancellationToken);
    }
}
