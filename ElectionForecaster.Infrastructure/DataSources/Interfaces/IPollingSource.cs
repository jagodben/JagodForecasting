using ElectionForecaster.Infrastructure.DataSources.Models;

namespace ElectionForecaster.Infrastructure.DataSources.Interfaces;

/// <summary>
/// Interface for fetching polling data.
/// </summary>
public interface IPollingSource
{
    /// <summary>
    /// Gets the name of this polling source.
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Gets the weighted polling average for a specific race.
    /// </summary>
    Task<PollingAverage?> GetPollingAverageAsync(string raceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent individual polls for a specific race.
    /// </summary>
    Task<List<PollData>> GetRecentPollsAsync(string raceId, int days = 30, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets polling averages for all available races.
    /// </summary>
    Task<Dictionary<string, PollingAverage>> GetAllPollingAveragesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the cached polling data from the source.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
