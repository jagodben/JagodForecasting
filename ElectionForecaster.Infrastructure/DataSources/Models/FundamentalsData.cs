using ElectionForecaster.Core.Enums;

namespace ElectionForecaster.Infrastructure.DataSources.Models;

/// <summary>
/// Represents fundamental/structural factors for a race forecast.
/// </summary>
public class FundamentalsData
{
    /// <summary>
    /// The race identifier.
    /// </summary>
    public string RaceId { get; set; } = string.Empty;

    /// <summary>
    /// Cook PVI (Partisan Voting Index) for the state/district.
    /// Positive = Dem lean (e.g., D+5), negative = Rep lean (e.g., R+3 = -3).
    /// </summary>
    public double PartisanLean { get; set; }

    /// <summary>
    /// Generic congressional ballot margin.
    /// Positive = Dem lead, negative = Rep lead.
    /// </summary>
    public double GenericBallot { get; set; }

    /// <summary>
    /// Whether the incumbent is a Democrat (true), Republican (false), or no incumbent (null).
    /// </summary>
    public bool? IncumbentIsDem { get; set; }

    /// <summary>
    /// Incumbency advantage in percentage points.
    /// </summary>
    public double IncumbencyAdvantage { get; set; }

    /// <summary>
    /// Whether this is a midterm election with a presidential penalty.
    /// </summary>
    public bool IsMidterm { get; set; }

    /// <summary>
    /// The president's party (for midterm penalty calculation).
    /// </summary>
    public Party? PresidentParty { get; set; }

    /// <summary>
    /// Expected midterm penalty (in terms of national environment shift).
    /// </summary>
    public double MidtermPenalty { get; set; }

    /// <summary>
    /// Calculates the fundamentals-based expected margin for Democrats.
    /// </summary>
    public double GetExpectedDemMargin()
    {
        double margin = PartisanLean;

        // Add generic ballot effect (adjusted for the election type)
        margin += GenericBallot;

        // Add incumbency advantage
        if (IncumbentIsDem == true)
            margin += IncumbencyAdvantage;
        else if (IncumbentIsDem == false)
            margin -= IncumbencyAdvantage;

        // Apply midterm penalty to president's party
        if (IsMidterm && PresidentParty.HasValue)
        {
            if (PresidentParty == Party.Democrat)
                margin -= MidtermPenalty;
            else if (PresidentParty == Party.Republican)
                margin += MidtermPenalty;
        }

        return margin;
    }

    /// <summary>
    /// Converts expected margin to win probability.
    /// </summary>
    public double GetDemWinProbability()
    {
        double margin = GetExpectedDemMargin();
        // Assume ~5% standard deviation for fundamentals-based predictions
        const double standardError = 5.0;
        double zScore = margin / standardError;
        return NormalCdf(zScore);
    }

    public double GetRepWinProbability() => 1.0 - GetDemWinProbability();

    private static double NormalCdf(double x)
    {
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
