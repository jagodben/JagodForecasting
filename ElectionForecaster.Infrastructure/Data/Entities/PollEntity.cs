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

    // Undecided-primary matchup blend: mean percentages across every hypothetical matchup the
    // pollster tested that day (null when only one), how many there were, and the max−min margin
    // spread between them. DemPercent/RepPercent stay the first-listed (displayed) matchup.
    public double? BlendDemPercent { get; set; }
    public double? BlendRepPercent { get; set; }
    public int MatchupCount { get; set; } = 1;
    public double MatchupSpread { get; set; }

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
