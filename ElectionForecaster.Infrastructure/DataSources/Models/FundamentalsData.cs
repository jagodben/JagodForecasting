using ElectionForecaster.Infrastructure.Forecasting;

namespace ElectionForecaster.Infrastructure.DataSources.Models;

/// <summary>
/// Structural (non-poll) factors for a race, expressed so they compose into a single
/// expected Democratic margin. The national environment is captured by one term
/// (<see cref="NationalEnvironment"/>) — either the generic-ballot average or, absent
/// that, an approval-based projection that already includes the midterm effect. It is
/// NOT added on top of a separate midterm penalty (that would double-count the mood).
/// </summary>
public class FundamentalsData
{
    public string RaceId { get; set; } = string.Empty;

    /// <summary>
    /// Cook PVI for the state/district. Positive = Dem lean (D+5), negative = Rep lean (R+3 = -3).
    /// Defined relative to a neutral national environment, so it's the clean baseline.
    /// </summary>
    public double PartisanLean { get; set; }

    /// <summary>
    /// The national mood as a Dem margin in points (e.g. +4 = D+4 environment). Sourced from
    /// the generic ballot when available, otherwise projected from presidential approval and
    /// the midterm penalty. Already contains the midterm effect — do not add a penalty on top.
    /// </summary>
    public double NationalEnvironment { get; set; }

    /// <summary>Whether the incumbent is a Democrat (true), Republican (false), or open seat (null).</summary>
    public bool? IncumbentIsDem { get; set; }

    /// <summary>Incumbency advantage magnitude, in points (added toward the incumbent's party).</summary>
    public double IncumbencyAdvantage { get; set; }

    /// <summary>
    /// Fundamentals-only expected Democratic margin (points): partisan lean + national
    /// environment + incumbency, in a uniform-swing framing.
    /// </summary>
    public double GetExpectedDemMargin()
    {
        double margin = PartisanLean + NationalEnvironment;
        if (IncumbentIsDem == true) margin += IncumbencyAdvantage;
        else if (IncumbentIsDem == false) margin -= IncumbencyAdvantage;
        return margin;
    }

    /// <summary>Fundamentals-only Dem win probability at the given margin standard error.</summary>
    public double GetDemWinProbability(double standardError = 6.0)
        => ForecastMath.MarginToProbability(GetExpectedDemMargin(), standardError);

    public double GetRepWinProbability(double standardError = 6.0)
        => 1.0 - GetDemWinProbability(standardError);
}
