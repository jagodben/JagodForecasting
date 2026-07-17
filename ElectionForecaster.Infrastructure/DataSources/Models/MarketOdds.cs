namespace ElectionForecaster.Infrastructure.DataSources.Models;

/// <summary>Prediction market odds for a race. Odds are win probabilities 0..1.</summary>
public class MarketOdds
{
    public string RaceId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public double DemOdds { get; set; }
    public double RepOdds { get; set; }
    public DateTime Timestamp { get; set; }
    public double? Volume { get; set; }
    public string? ExternalMarketId { get; set; }

    /// <summary>Confidence in this data based on trading volume (higher = more reliable signal).</summary>
    public double Confidence => CalculateConfidence();

    private double CalculateConfidence()
    {
        if (!Volume.HasValue || Volume.Value <= 0)
            return 0.5;

        // $100k volume ≈ 0.8, $1M+ ≈ 1.0
        return Math.Min(1.0, 0.5 + (Math.Log10(Volume.Value + 1) / 14));
    }
}
