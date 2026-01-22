using System.ComponentModel.DataAnnotations;

namespace ElectionForecaster.Infrastructure.Data.Entities;

/// <summary>
/// Stores daily chamber control forecasts for Senate and House.
/// </summary>
public class ChamberHistoryEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string Chamber { get; set; } = string.Empty; // "Senate" or "House"

    [Required]
    public DateTime Date { get; set; }

    public double DemControlProbability { get; set; }
    public double RepControlProbability { get; set; }

    public double ExpectedDemSeats { get; set; }
    public double ExpectedRepSeats { get; set; }

    // Simulation metadata
    public int SimulationIterations { get; set; }

    // Confidence interval
    public int DemSeatsLow { get; set; }  // 10th percentile
    public int DemSeatsHigh { get; set; } // 90th percentile
}
