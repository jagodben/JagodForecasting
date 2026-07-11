using ElectionForecaster.Infrastructure.Forecasting;

namespace ElectionForecaster.Infrastructure.DataSources.Models;

/// <summary>
/// Structural (non-poll) factors for a race, composed into an expected Democratic margin.
/// The national mood lives in one term (<see cref="NationalEnvironment"/>) — never add a
/// separate midterm penalty on top, that would double-count it.
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
    /// Fraction of a running incumbent's past overperformance (beyond the flat incumbency term)
    /// that carries forward. Backtested on 2018/2022 statewide races: 0.30–0.40 is the optimum,
    /// and open seats must retain nothing. See ElectionForecaster.Backtest.
    /// </summary>
    private const double PriorExcessRetention = 0.35;

    /// <summary>Keeps a single seat's fundamentals margin from running away on an extreme prior.</summary>
    private const double MaxFundamentalsMargin = 40.0;

    /// <summary>
    /// Fundamentals-only expected Democratic margin (points): PVI + national environment ± flat
    /// incumbency, plus a retained slice of a running incumbent's past overperformance (so
    /// crossover incumbents like Scott or Manchin aren't assumed to vote the seat's PVI).
    /// Open seats ignore the prior — the personal vote leaves with the departing incumbent.
    /// </summary>
    public double GetExpectedDemMargin()
    {
        double incumbency = IncumbentIsDem == true ? IncumbencyAdvantage
                          : IncumbentIsDem == false ? -IncumbencyAdvantage
                          : 0.0;
        double structural = PartisanLean + NationalEnvironment + incumbency;

        if (PriorMargin.HasValue && IncumbentIsDem.HasValue)
        {
            double excess = PriorMargin.Value - PartisanLean - incumbency;
            structural += PriorExcessRetention * excess;
        }

        return Math.Clamp(structural, -MaxFundamentalsMargin, MaxFundamentalsMargin);
    }

    /// <summary>Fundamentals-only Dem win probability at the given margin standard error.</summary>
    public double GetDemWinProbability(double standardError = 6.0)
        => ForecastMath.MarginToProbability(GetExpectedDemMargin(), standardError);

    public double GetRepWinProbability(double standardError = 6.0)
        => 1.0 - GetDemWinProbability(standardError);
}
