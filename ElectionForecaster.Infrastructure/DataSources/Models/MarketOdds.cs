namespace ElectionForecaster.Infrastructure.DataSources.Models;

/// <summary>
/// Represents prediction market odds for a race.
/// </summary>
public class MarketOdds
{
    /// <summary>
    /// The race identifier.
    /// </summary>
    public string RaceId { get; set; } = string.Empty;

    /// <summary>
    /// The source of the market data (e.g., "Polymarket", "Kalshi").
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Democratic candidate win probability (0.0 to 1.0).
    /// </summary>
    public double DemOdds { get; set; }

    /// <summary>
    /// Republican candidate win probability (0.0 to 1.0).
    /// </summary>
    public double RepOdds { get; set; }

    /// <summary>
    /// When this data was fetched.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Trading volume/liquidity (higher = more reliable signal).
    /// </summary>
    public double? Volume { get; set; }

    /// <summary>
    /// The external market ID from the source platform.
    /// </summary>
    public string? ExternalMarketId { get; set; }

    /// <summary>
    /// Confidence in this data based on volume and market maturity.
    /// </summary>
    public double Confidence => CalculateConfidence();

    private double CalculateConfidence()
    {
        if (!Volume.HasValue || Volume.Value <= 0)
            return 0.5; // Low confidence if no volume data

        // Higher volume = higher confidence, capped at 1.0
        // $100k volume = 0.8 confidence, $1M+ = ~1.0 confidence
        return Math.Min(1.0, 0.5 + (Math.Log10(Volume.Value + 1) / 14));
    }
}
