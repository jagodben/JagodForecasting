using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Models;

namespace ElectionForecaster.Infrastructure.Forecasting;

/// <summary>
/// Interface for the main forecasting engine that combines all data sources.
/// </summary>
public interface IForecastingOrchestrator
{
    /// <summary>
    /// Generates a detailed forecast for a specific race.
    /// </summary>
    Task<DetailedForecast> GenerateForecastAsync(string raceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates forecasts for all races of a specific type.
    /// </summary>
    Task<List<DetailedForecast>> GenerateAllForecastsAsync(RaceType? raceType = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Simulates chamber control using Monte Carlo simulation.
    /// </summary>
    Task<ChamberForecast> SimulateChamberAsync(RaceType chamber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the stored chamber control-over-time history (cheap DB read, no simulation).
    /// </summary>
    Task<List<ChamberHistoryPoint>> GetChamberHistoryAsync(string chamber, int days, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes all data sources.
    /// </summary>
    Task RefreshAllDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores daily snapshot of forecasts to the database.
    /// </summary>
    Task StoreDailySnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Backfills the full model's forecast history retrospectively (statewide races), reconstructing
    /// each day's market and polling inputs so the stored history reflects what the model would have said.
    /// </summary>
    Task BackfillModelHistoryAsync(CancellationToken cancellationToken = default);
}
