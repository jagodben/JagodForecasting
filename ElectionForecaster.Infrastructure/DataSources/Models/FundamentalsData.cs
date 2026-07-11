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
    /// How much of a running incumbent's past overperformance — beyond the flat incumbency term —
    /// carries to the next election. Backtested on 2018/2022 statewide races
    /// (ElectionForecaster.Backtest): 0.30–0.40 is the calibrated optimum; this hybrid form beat
    /// both the flat-incumbency-only model and raw-prior retention on MAE, RMSE, Brier, and
    /// log-loss. Open seats retain NOTHING (validated: the personal vote leaves with the departing
    /// incumbent — carried priors made open-seat crossover states like 2022 MA/MD much worse).
    /// </summary>
    private const double PriorExcessRetention = 0.35;

    /// <summary>Keeps a single seat's fundamentals margin from running away on an extreme prior.</summary>
    private const double MaxFundamentalsMargin = 40.0;

    /// <summary>
    /// Fundamentals-only expected Democratic margin (points): PVI + national environment ± the
    /// flat incumbency term, plus — when the incumbent is running and the seat has a prior result —
    /// a retained fraction of their past overperformance BEYOND that flat term, so crossover
    /// incumbents (a Phil Scott or a Joe Manchin) are reflected rather than assumed to vote the
    /// seat's PVI. Open seats ignore the prior entirely: the personal vote leaves with the
    /// departing incumbent (both rules backtested on 2018/2022; see ElectionForecaster.Backtest).
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
