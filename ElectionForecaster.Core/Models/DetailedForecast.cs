namespace ElectionForecaster.Core.Models;

/// <summary>
/// Detailed forecast including input breakdown and historical data.
/// </summary>
public class DetailedForecast
{
    public string RaceId { get; set; } = string.Empty;
    public double DemWinProbability { get; set; }
    public double RepWinProbability { get; set; }
    public double DemVoteShare { get; set; }
    public double RepVoteShare { get; set; }
    public double Confidence { get; set; }
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Breakdown of inputs used in this forecast.
    /// </summary>
    public ForecastInputs Inputs { get; set; } = new();

    /// <summary>
    /// Historical data points for charting.
    /// </summary>
    public List<HistoricalDataPoint> History { get; set; } = new();
}

/// <summary>
/// Individual inputs that comprise a forecast.
/// </summary>
public class ForecastInputs
{
    // Raw input values (null if data unavailable)
    public double? MarketOdds { get; set; }
    public double? PollingAverage { get; set; }
    /// <summary>Dem win probability derived from the polling margin (0..1). Null if no polls.</summary>
    public double? PollingWinProbability { get; set; }
    public double? FundamentalsPrediction { get; set; }
    public double? ApprovalAdjustment { get; set; }

    // Weights used for each input
    public double MarketWeight { get; set; }
    public double PollingWeight { get; set; }
    public double FundamentalsWeight { get; set; }
    public double ApprovalWeight { get; set; }

    // Data quality/freshness indicators
    public DateTime? MarketLastUpdated { get; set; }
    public DateTime? PollingLastUpdated { get; set; }
    public int? PollCount { get; set; }
}

/// <summary>
/// A single point in the forecast history for charting.
/// </summary>
public class HistoricalDataPoint
{
    public DateTime Date { get; set; }
    public double DemWinProbability { get; set; }
    public double RepWinProbability { get; set; }
    public double? DemVoteShare { get; set; }
    public double? RepVoteShare { get; set; }
}
