using ElectionForecaster.Infrastructure.DataSources.Models;

namespace ElectionForecaster.Infrastructure.DataSources.Polling;

/// <summary>
/// Estimates each pollster's "house effect" — the systematic partisan lean of its results
/// relative to the consensus of the other pollsters on the same races. Only public
/// general-election polls feed the estimate; partisan-sponsored polls are excluded, since
/// their lean is a deliberate choice, not a house effect. Estimates are measured against the
/// field (so they're relative, and roughly sum to zero within a race), shrunk toward zero by
/// sample size, and clamped — a pollster with only a poll or two barely moves.
///
/// This is a single-pass, leave-one-out estimate, not the iterative fixed point 538 used
/// (re-deriving the consensus after de-biasing and repeating). It's enough to correct the
/// gross composition bias that appears when a race is polled mostly by one-leaning firms.
/// </summary>
public static class PollsterHouseEffects
{
    private const int MinPolls = 3;        // need at least this many comparisons before trusting a lean
    private const double ShrinkageK = 4.0; // pulls small-sample estimates toward zero
    private const double MaxEffect = 6.0;  // cap the correction (margin points) against a bad estimate

    /// <summary>
    /// Pollster name → estimated Dem-favorable lean in margin points (positive = leans Democratic).
    /// Subtract this from a pollster's polls to de-bias them. Pollsters below the poll-count floor
    /// are omitted; callers treat a missing entry as zero.
    /// </summary>
    public static Dictionary<string, double> Estimate(IReadOnlyList<PollData> polls)
    {
        var sums = new Dictionary<string, (double SumDev, int Count)>(StringComparer.OrdinalIgnoreCase);

        foreach (var raceGroup in polls.Where(p => !p.IsPartisan).GroupBy(p => p.RaceId))
        {
            var racePolls = raceGroup.ToList();
            if (racePolls.Count < 2) continue; // no consensus to compare against

            double totalMargin = racePolls.Sum(p => p.Margin);
            int n = racePolls.Count;

            foreach (var poll in racePolls)
            {
                // Leave-one-out consensus: the mean margin of the *other* polls on this race.
                double consensus = (totalMargin - poll.Margin) / (n - 1);
                double dev = poll.Margin - consensus;

                var cur = sums.TryGetValue(poll.Pollster, out var v) ? v : (SumDev: 0.0, Count: 0);
                sums[poll.Pollster] = (cur.SumDev + dev, cur.Count + 1);
            }
        }

        var effects = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (pollster, agg) in sums)
        {
            if (agg.Count < MinPolls) continue;
            // Shrink toward zero: SumDev/(Count+K) == meanDeviation * Count/(Count+K).
            double effect = Math.Clamp(agg.SumDev / (agg.Count + ShrinkageK), -MaxEffect, MaxEffect);
            effects[pollster] = effect;
        }

        return effects;
    }
}
