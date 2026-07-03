using ElectionForecaster.Core.Enums;

namespace ElectionForecaster.Infrastructure.Forecasting;

/// <summary>
/// Estimates the standard error (in margin points) of a race forecast. Uncertainty
/// shrinks as the election approaches and as more polling accumulates, but never to
/// zero — elections retain irreducible uncertainty. Empirically, forecast MAE runs
/// ~3pts at two months out and ~1.5pts in the final days; the predictive SD is wider
/// because it must also cover model error and correlated polling misses.
/// </summary>
public static class UncertaintyModel
{
    /// <summary>Systematic (correlated across races) component of the error, in margin points.</summary>
    public const double NationalErrorStdDev = 3.0;

    /// <summary>Lowest a race's total SD can go — no race is ever a certainty.</summary>
    private const double FloorStdDev = 3.5;

    public static double MarginStandardError(double daysToElection, RaceType raceType, int pollCount)
    {
        // Base uncertainty by time to election. Calibrated against the 2018/2022 backtest, where the
        // fundamentals-only RMSE was ~8pts — so a no-poll race far out should sit near there, not the
        // overconfident ~6 the first cut used (that produced 96% on 2-poll races).
        double se = daysToElection switch
        {
            > 180 => 8.5,
            > 60 => 7.5,
            > 14 => 5.5,
            _ => 4.5
        };

        // Governors are far noisier than Senate (crossover / personal-vote incumbents) — backtest
        // RMSE was ~10 for governors vs ~6.4 for Senate. House seats are sparsely polled and volatile.
        se += raceType switch
        {
            RaceType.Governor => 2.5,
            RaceType.House => 1.5,
            _ => 0.0
        };

        // Polling reduces uncertainty with diminishing returns — poll errors are correlated, so the
        // tenth poll barely helps (cube-root scaling, as FiveThirtyEight uses). A gentler coefficient
        // keeps a couple of early polls from collapsing the SE (and overstating confidence).
        if (pollCount > 0)
        {
            se -= 1.2 * Math.Cbrt(pollCount);
        }

        return Math.Max(FloorStdDev, se);
    }
}
