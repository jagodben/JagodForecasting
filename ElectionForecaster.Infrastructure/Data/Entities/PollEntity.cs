using System.ComponentModel.DataAnnotations;

namespace ElectionForecaster.Infrastructure.Data.Entities;

/// <summary>
/// Stores raw polling data from various pollsters.
/// </summary>
public class PollEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string RaceId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Pollster { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    public int? SampleSize { get; set; }

    public double DemPercent { get; set; }
    public double RepPercent { get; set; }

    // Pollster quality rating (e.g., "A+", "B-", etc.)
    [MaxLength(10)]
    public string? PollsterRating { get; set; }

    // Poll methodology
    [MaxLength(100)]
    public string? Methodology { get; set; }

    // Population type (LV = Likely Voters, RV = Registered Voters, A = Adults)
    [MaxLength(10)]
    public string? Population { get; set; }

    // Source URL for verification
    [MaxLength(500)]
    public string? SourceUrl { get; set; }
}
