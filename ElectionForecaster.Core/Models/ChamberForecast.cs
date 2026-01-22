namespace ElectionForecaster.Core.Models;

/// <summary>
/// Forecast for overall chamber control (Senate or House).
/// </summary>
public class ChamberForecast
{
    public string Chamber { get; set; } = string.Empty; // "Senate" or "House"
    public double DemControlProbability { get; set; }
    public double RepControlProbability { get; set; }
    public double ExpectedDemSeats { get; set; }
    public double ExpectedRepSeats { get; set; }
    public int SeatsNeededForControl { get; set; }
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Confidence interval for Democratic seats.
    /// </summary>
    public SeatRange DemSeatRange { get; set; } = new();

    /// <summary>
    /// Number of Monte Carlo iterations used.
    /// </summary>
    public int SimulationIterations { get; set; }

    /// <summary>
    /// Historical chamber control probability for charting.
    /// </summary>
    public List<ChamberHistoryPoint> History { get; set; } = new();
}

/// <summary>
/// Range of expected seats with confidence levels.
/// </summary>
public class SeatRange
{
    public int Low { get; set; }   // 10th percentile
    public int High { get; set; }  // 90th percentile
    public int Median { get; set; }
}

/// <summary>
/// Historical chamber control data point.
/// </summary>
public class ChamberHistoryPoint
{
    public DateTime Date { get; set; }
    public double DemControlProbability { get; set; }
    public double ExpectedDemSeats { get; set; }
}
