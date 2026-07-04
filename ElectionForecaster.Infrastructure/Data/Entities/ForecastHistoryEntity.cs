using System.ComponentModel.DataAnnotations;

namespace ElectionForecaster.Infrastructure.Data.Entities;

/// <summary>
/// Stores daily forecast snapshots for tracking historical changes.
/// </summary>
public class ForecastHistoryEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string RaceId { get; set; } = string.Empty;

    [Required]
    public DateTime Date { get; set; }

    public double DemWinProbability { get; set; }
    public double RepWinProbability { get; set; }
    public double DemVoteShare { get; set; }
    public double RepVoteShare { get; set; }
    public double Confidence { get; set; }

    // Blended expected Dem margin (points) and its standard error at the time of the forecast.
    public double ExpectedDemMargin { get; set; }
    public double MarginStdDev { get; set; }

    // Weight breakdown for transparency
    public double MarketWeight { get; set; }
    public double PollingWeight { get; set; }
    public double FundamentalsWeight { get; set; }
    public double ApprovalWeight { get; set; }

    // Input values at time of forecast
    public double? MarketOdds { get; set; }
    public double? PollingAverage { get; set; }
    public double? FundamentalsPrediction { get; set; }
    public double? ApprovalAdjustment { get; set; }
}
