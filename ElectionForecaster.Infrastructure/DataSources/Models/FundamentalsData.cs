using ElectionForecaster.Infrastructure.Forecasting;

namespace ElectionForecaster.Infrastructure.DataSources.Models;

/// <summary>
/// Structural (non-poll) factors for a race, expressed so they compose into a single
/// expected Democratic margin. The national environment is captured by one term
/// (<see cref="NationalEnvironment"/>) — either the generic-ballot average or, absent
/// that, a baseline midterm out-party bonus that already includes the midterm effect. It is
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
    /// the generic ballot when available, otherwise a baseline midterm out-party bonus.
    /// Already contains the midterm effect — do not add a penalty on top.
    /// </summary>
    public double NationalEnvironment { get; set; }

    /// <summary>Whether the incumbent is a Democrat (true), Republican (false), or open seat (null).</summary>
    public bool? IncumbentIsDem { get; set; }

    /// <summary>Incumbency advantage magnitude, in points (added toward the incumbent's party).</summary>
    public double IncumbencyAdvantage { get; set; }

    /// <summary>
    /// The seat's most recent comparable Dem margin (points), when known (statewide races). Captures
    /// the demonstrated lean beyond PVI — a crossover incumbent or a lopsided safe seat — that a pure
    /// PVI model can't see. Null for races we have no prior for (e.g. House).
    /// </summary>
    public double? PriorMargin { get; set; }

    /// <summary>
    /// How much of a seat's past deviation from its PVI persists to the next election. Landslides
    /// mean-revert (a new opponent, the incumbent's personal vote fades), so only a fraction carries.
    /// ~0.45 is mid-range between "sticks entirely" and "reverts fully to PVI".
    /// </summary>
    private const double PriorResultRetention = 0.45;

    /// <summary>
    /// Retention for an open seat (no incumbent running). Most of a seat's past overperformance is
    /// the departed incumbent's personal vote, which leaves with them — keep only a sliver for the
    /// seat's residual non-presidential lean (party infrastructure, downballot habits).
    /// </summary>
    private const double PriorResultRetentionOpenSeat = 0.25;

    /// <summary>Keeps a single seat's fundamentals margin from running away on an extreme prior.</summary>
    private const double MaxFundamentalsMargin = 40.0;

    /// <summary>
    /// Fundamentals-only expected Democratic margin (points). When a prior result is known, the
    /// margin is PVI + national environment + a retained fraction of the seat's past deviation from
    /// PVI — so a crossover incumbent or safe-seat lean is reflected rather than assuming the seat
    /// votes its presidential PVI. The retained fraction is smaller for open seats, where the past
    /// overperformance was mostly the departed incumbent's personal vote. Without a prior, falls
    /// back to PVI + national ± flat incumbency.
    /// </summary>
    public double GetExpectedDemMargin()
    {
        double structural = PartisanLean + NationalEnvironment;

        if (PriorMargin.HasValue)
        {
            double retention = IncumbentIsDem.HasValue ? PriorResultRetention : PriorResultRetentionOpenSeat;
            double overPerformance = PriorMargin.Value - PartisanLean;
            double margin = structural + retention * overPerformance;
            return Math.Clamp(margin, -MaxFundamentalsMargin, MaxFundamentalsMargin);
        }

        if (IncumbentIsDem == true) structural += IncumbencyAdvantage;
        else if (IncumbentIsDem == false) structural -= IncumbencyAdvantage;
        return structural;
    }

    /// <summary>Fundamentals-only Dem win probability at the given margin standard error.</summary>
    public double GetDemWinProbability(double standardError = 6.0)
        => ForecastMath.MarginToProbability(GetExpectedDemMargin(), standardError);

    public double GetRepWinProbability(double standardError = 6.0)
        => 1.0 - GetDemWinProbability(standardError);
}
