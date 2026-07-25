using System.Text.RegularExpressions;
using ElectionForecaster.Infrastructure.DataSources.Models;

namespace ElectionForecaster.Infrastructure.DataSources.Polling;

/// <summary>
/// Collapses the multiple hypothetical-matchup tables an undecided-primary race is polled under
/// into one row per (pollster, date).
///
/// While a primary is undecided (no settled nominee for that side), every matchup the pollster
/// tested counts: the first-listed matchup keeps the display percentages (that's the row the polls
/// page shows), the model-facing blend fields average across the matchups, and the margin spread
/// between them is recorded so the orchestrator can price nominee uncertainty as extra standard
/// error. Once a side's nominee is settled, only matchups featuring that nominee count — a stale
/// hypothetical must not keep speaking for the race — and with both sides settled the poll
/// collapses to the single real-matchup row. Races polled under one table pass through unchanged.
/// </summary>
public static class MatchupBlender
{
    public static List<PollData> Blend(
        IReadOnlyList<(PollData Poll, int TableIndex)> rows,
        string? demNominee = null,
        string? repNominee = null)
    {
        // Within one table a pollster/date can repeat (an LV line then an RV line) — keep the
        // first, matching the old global keep-first behavior.
        var perTable = rows
            .GroupBy(r => (r.Poll.Pollster, Date: r.Poll.Date.Date, r.TableIndex))
            .Select(g => g.First());

        return perTable
            .GroupBy(r => (r.Poll.Pollster, Date: r.Poll.Date.Date))
            .Select(g =>
            {
                var matchups = g.OrderBy(r => r.TableIndex).Select(r => r.Poll).ToList();

                // Settled primaries prune the matchup set. If nothing matches (the nominee was
                // never polled by name), keep the full set — a blend beats a blind first-table pick.
                var filtered = matchups
                    .Where(m => MatchesNominee(m.DemCandidate, demNominee)
                             && MatchesNominee(m.RepCandidate, repNominee))
                    .ToList();
                if (filtered.Count > 0) matchups = filtered;

                var primary = matchups[0];
                if (matchups.Count > 1)
                {
                    primary.BlendDemPercent = matchups.Average(m => m.DemPercent);
                    primary.BlendRepPercent = matchups.Average(m => m.RepPercent);
                    primary.MatchupCount = matchups.Count;
                    primary.MatchupSpread = matchups.Max(m => m.Margin) - matchups.Min(m => m.Margin);
                }
                return primary;
            })
            .ToList();
    }

    /// <summary>
    /// Whether a matchup column's candidate is the settled nominee. No settled nominee → every
    /// matchup qualifies. Containment (after normalizing whitespace/case) absorbs suffix and
    /// middle-name variations like "Nick Begich III" vs "Nick Begich".
    /// </summary>
    private static bool MatchesNominee(string? candidate, string? nominee)
    {
        if (nominee is null) return true;
        if (candidate is null) return false;
        var c = Normalize(candidate);
        var n = Normalize(nominee);
        return c.Contains(n) || n.Contains(c);
    }

    private static string Normalize(string s) => Regex.Replace(s.Trim().ToLowerInvariant(), @"\s+", " ");
}
