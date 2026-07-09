using ElectionForecaster.Infrastructure.Forecasting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ElectionForecaster.Infrastructure.Services;

/// <summary>
/// Background service that periodically refreshes forecast data from all sources.
/// </summary>
public class DataRefreshService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataRefreshService> _logger;
    private readonly TimeSpan _marketRefreshInterval;
    private readonly TimeSpan _pollingRefreshInterval;
    private readonly TimeSpan _snapshotInterval;

    private DateTime _lastMarketRefresh = DateTime.MinValue;
    private DateTime _lastPollingRefresh = DateTime.MinValue;
    private DateTime _lastSnapshot = DateTime.MinValue;
    private bool _backfillComplete = false;

    public DataRefreshService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<DataRefreshService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Get refresh intervals from configuration
        var forecastingSection = configuration.GetSection("Forecasting:RefreshIntervals");
        _marketRefreshInterval = TimeSpan.FromMinutes(
            forecastingSection.GetValue<int>("PredictionMarketsMinutes", 15));
        _pollingRefreshInterval = TimeSpan.FromHours(
            forecastingSection.GetValue<int>("PollingHours", 6));
        _snapshotInterval = TimeSpan.FromHours(24); // Daily snapshots
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Data refresh service starting...");

        // Initial delay to let the application fully start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshDataAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in data refresh cycle");
            }

            // Check every minute for refresh needs
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        _logger.LogInformation("Data refresh service stopping...");
    }

    private async Task RefreshDataAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IForecastingOrchestrator>();

        var now = DateTime.UtcNow;

        // One-time retrospective backfill of the model's forecast history (statewide, from June 1).
        if (!_backfillComplete)
        {
            _logger.LogInformation("Running one-time model history backfill...");
            try
            {
                await orchestrator.BackfillModelHistoryAsync(force: false, cancellationToken);
                _backfillComplete = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to backfill model history");
                _backfillComplete = true; // Don't retry on failure
            }
        }

        // Check if market refresh is needed
        if (now - _lastMarketRefresh > _marketRefreshInterval)
        {
            _logger.LogInformation("Refreshing prediction market data...");
            try
            {
                await orchestrator.RefreshAllDataAsync(cancellationToken);
                _lastMarketRefresh = now;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh market data");
            }
        }

        // Check if polling refresh is needed
        if (now - _lastPollingRefresh > _pollingRefreshInterval)
        {
            _logger.LogInformation("Refreshing polling data...");
            try
            {
                await orchestrator.RefreshAllDataAsync(cancellationToken);
                _lastPollingRefresh = now;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh polling data");
            }
        }

        // Check if daily snapshot is needed
        if (now - _lastSnapshot > _snapshotInterval || _lastSnapshot.Date != now.Date)
        {
            // Only store one snapshot per day
            if (_lastSnapshot.Date != now.Date)
            {
                _logger.LogInformation("Storing daily forecast snapshot...");
                try
                {
                    await orchestrator.StoreDailySnapshotAsync(cancellationToken);
                    _lastSnapshot = now;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to store daily snapshot");
                }
            }
        }
    }
}
