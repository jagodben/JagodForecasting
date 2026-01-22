namespace ElectionForecaster.Infrastructure.DataSources.Models;

/// <summary>
/// Represents a weighted polling average for a race.
/// </summary>
public class PollingAverage
{
    /// <summary>
    /// The race identifier.
    /// </summary>
    public string RaceId { get; set; } = string.Empty;

    /// <summary>
    /// Democratic candidate polling average (0.0 to 100.0).
    /// </summary>
    public double DemPercent { get; set; }

    /// <summary>
    /// Republican candidate polling average (0.0 to 100.0).
    /// </summary>
    public double RepPercent { get; set; }

    /// <summary>
    /// The margin (positive = Dem lead, negative = Rep lead).
    /// </summary>
    public double Margin => DemPercent - RepPercent;

    /// <summary>
    /// Number of polls included in the average.
    /// </summary>
    public int PollCount { get; set; }

    /// <summary>
    /// Date of the most recent poll included.
    /// </summary>
    public DateTime? LatestPollDate { get; set; }

    /// <summary>
    /// Average sample size of included polls.
    /// </summary>
    public int? AverageSampleSize { get; set; }

    /// <summary>
    /// Confidence in this average based on poll quantity and recency.
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Converts polling average to win probability using a simple model.
    /// </summary>
    public double GetDemWinProbability()
    {
        // Use a normal distribution approximation
        // Assume ~3.5% standard error for polling
        const double standardError = 3.5;
        double zScore = Margin / standardError;
        return NormalCdf(zScore);
    }

    public double GetRepWinProbability() => 1.0 - GetDemWinProbability();

    private static double NormalCdf(double x)
    {
        // Approximation of the cumulative distribution function
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        int sign = x < 0 ? -1 : 1;
        x = Math.Abs(x) / Math.Sqrt(2);

        double t = 1.0 / (1.0 + p * x);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

        return 0.5 * (1.0 + sign * y);
    }
}
