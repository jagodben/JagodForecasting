using ElectionForecaster.Infrastructure.DataSources.Models;

namespace ElectionForecaster.Infrastructure.DataSources.Interfaces;

/// <summary>
/// Interface for fetching prediction market odds.
/// </summary>
public interface IPredictionMarketSource
{
    /// <summary>
    /// Gets the name of this market source (e.g., "Polymarket", "Kalshi").
    /// </summary>
    string SourceName { get; }

    /// <summary>
    /// Gets the current market odds for a specific race.
    /// </summary>
    Task<MarketOdds?> GetRaceOddsAsync(string raceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current market odds for all available races.
    /// </summary>
    Task<Dictionary<string, MarketOdds>> GetAllRaceOddsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the cached market data from the source.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
