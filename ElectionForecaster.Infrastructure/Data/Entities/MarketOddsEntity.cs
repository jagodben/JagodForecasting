using System.ComponentModel.DataAnnotations;

namespace ElectionForecaster.Infrastructure.Data.Entities;

/// <summary>
/// Stores prediction market odds from various sources.
/// </summary>
public class MarketOddsEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string RaceId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Source { get; set; } = string.Empty; // "Polymarket", "Kalshi", etc.

    [Required]
    public DateTime Timestamp { get; set; }

    public double DemOdds { get; set; } // 0.0 to 1.0
    public double RepOdds { get; set; } // 0.0 to 1.0

    // Market liquidity/volume (higher = more reliable)
    public double? Volume { get; set; }

    // The specific market ID from the source
    [MaxLength(200)]
    public string? ExternalMarketId { get; set; }
}
