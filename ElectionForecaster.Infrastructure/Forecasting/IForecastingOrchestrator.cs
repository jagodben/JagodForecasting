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
    Task<List<ChamberHistoryPoint>> GetChamberHistoryAsync(string chamber, CancellationToken cancellationToken = default);

    Task RefreshAllDataAsync(CancellationToken cancellationToken = default);

    Task StoreDailySnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The once-a-day update (refresh all sources + store the 8 AM snapshot the site serves).
    /// No-ops and returns false if today's Eastern snapshot is already complete.
    /// </summary>
    Task<bool> RunDailyUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fills gaps in the forecast history retrospectively, reconstructing each missing day's
    /// market and polling inputs. Recorded days are immutable — no code path replaces a row that
    /// exists, so what the site published on a given day stands forever. Without
    /// <paramref name="force"/> only races with no history at all are processed (the automatic
    /// startup heal); with it, every race is scanned for missing days.
    /// </summary>
    Task BackfillModelHistoryAsync(bool force = false, CancellationToken cancellationToken = default);
}
