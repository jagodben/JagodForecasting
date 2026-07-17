using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Models;

namespace ElectionForecaster.Infrastructure.Forecasting;

/// <summary>The main forecasting engine, blending every data source per race.</summary>
public interface IForecastingOrchestrator
{
    Task<DetailedForecast> GenerateForecastAsync(string raceId, CancellationToken cancellationToken = default);

    Task<List<DetailedForecast>> GenerateAllForecastsAsync(RaceType? raceType = null, CancellationToken cancellationToken = default);

    Task<ChamberForecast> SimulateChamberAsync(RaceType chamber, CancellationToken cancellationToken = default);

    /// <summary>Stored chamber control-over-time history (cheap DB read, no simulation).</summary>
    Task<List<ChamberHistoryPoint>> GetChamberHistoryAsync(string chamber, int days, CancellationToken cancellationToken = default);

    Task RefreshMarketDataAsync(CancellationToken cancellationToken = default);

    Task RefreshPollingDataAsync(CancellationToken cancellationToken = default);

    Task RefreshAllDataAsync(CancellationToken cancellationToken = default);

    Task StoreDailySnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The once-a-day update (refresh all sources + store the 8 AM snapshot the site serves).
    /// No-ops and returns false if today's Eastern snapshot is already complete.
    /// </summary>
    Task<bool> RunDailyUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Backfills forecast history retrospectively, reconstructing each day's market and polling
    /// inputs so the stored history reflects what the model would have said. Without
    /// <paramref name="force"/> it only seeds races that have no history rows at all (so the
    /// automatic startup call heals gaps without wiping genuine daily snapshots); with it, every
    /// race is deleted and rebuilt under the current model.
    /// </summary>
    Task BackfillModelHistoryAsync(bool force = false, CancellationToken cancellationToken = default);
}
