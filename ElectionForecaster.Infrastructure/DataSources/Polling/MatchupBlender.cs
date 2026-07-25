using ElectionForecaster.Infrastructure.DataSources.Models;

namespace ElectionForecaster.Infrastructure.DataSources.Polling;

/// <summary>
/// Collapses the multiple hypothetical-matchup tables an undecided-primary race is polled under
/// into one row per (pollster, date). The first-listed matchup keeps the display percentages
/// (that's the row the polls page shows, and post-primary Wikipedia lists the actual nominees
/// first); the model-facing blend fields average every matchup the pollster tested that day, and
/// the spread between matchups is recorded so the orchestrator can price nominee uncertainty as
/// extra standard error. Races with settled nominees have a single table and pass through unchanged.
/// </summary>
public static class MatchupBlender
{
    public static List<PollData> Blend(IReadOnlyList<(PollData Poll, int TableIndex)> rows)
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
}
