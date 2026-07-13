using ElectionForecaster.Infrastructure.Forecasting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ElectionForecaster.Infrastructure.Services;

/// <summary>
/// Background service that runs the once-a-day update. The site serves a forecast frozen to that
/// morning's 8 AM Eastern snapshot, so there's no need to refresh between snapshots — this just
/// wakes up, checks whether it's past 8 AM Eastern and today's snapshot is still missing, and if
/// so refreshes every source and stores the snapshot (exactly once per Eastern day).
/// </summary>
public class DataRefreshService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataRefreshService> _logger;
    private const int SnapshotHourEastern = 8;

    private static readonly TimeZoneInfo EasternZone = ResolveEastern();
    private DateTime _lastSnapshotEasternDate = DateTime.MinValue;
    private bool _backfillComplete = false;

    public DataRefreshService(IServiceProvider serviceProvider, ILogger<DataRefreshService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Data refresh service starting...");
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // let the app finish starting

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in data refresh cycle");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }

        _logger.LogInformation("Data refresh service stopping...");
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IForecastingOrchestrator>();

        // One-time retrospective backfill of the model's forecast history (only seeds an empty DB).
        if (!_backfillComplete)
        {
            try
            {
                // MODEL_REBACKFILL=1 (set for one deploy after a model change, then removed)
                // forces the retrospective history to be recomputed under the current model, so
                // charts don't show an artificial cliff where old-model history meets new points.
                var force = Environment.GetEnvironmentVariable("MODEL_REBACKFILL") == "1";
                await orchestrator.BackfillModelHistoryAsync(force, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to backfill model history");
            }
            _backfillComplete = true; // don't retry on failure
        }

        var nowEastern = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EasternZone);
        var easternToday = nowEastern.Date;

        // Already handled today this run, or it isn't 8 AM Eastern yet — nothing to do.
        if (_lastSnapshotEasternDate == easternToday || nowEastern.Hour < SnapshotHourEastern)
            return;

        // Refresh candidates from Wikipedia first, so today's snapshot (and the site) reflects
        // current nominees. A scrape failure must never block the snapshot itself.
        try
        {
            var candidateRefresh = scope.ServiceProvider.GetRequiredService<CandidateRefreshService>();
            var changes = await candidateRefresh.RefreshFromWikipediaAsync(cancellationToken);
            _logger.LogInformation("Daily candidate check complete — {Changes} candidate update(s)", changes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Daily candidate refresh failed; continuing with existing candidates");
        }

        try
        {
            // RunDailyUpdate no-ops if today's snapshot already exists (durable guard across
            // restarts), so the forecast is genuinely frozen at the morning's 8 AM value.
            var ran = await orchestrator.RunDailyUpdateAsync(cancellationToken);
            _lastSnapshotEasternDate = easternToday;
            _logger.LogInformation(ran
                ? "Daily 8 AM Eastern update complete for {Date:d}"
                : "Daily snapshot for {Date:d} already present — serving frozen forecast", easternToday);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to run the daily update");
        }
    }

    private static TimeZoneInfo ResolveEastern()
    {
        foreach (var id in new[] { "America/New_York", "Eastern Standard Time" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
        return TimeZoneInfo.Utc;
    }
}
