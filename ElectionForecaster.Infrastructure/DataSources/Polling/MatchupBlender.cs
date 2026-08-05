using System.Text.RegularExpressions;
using ElectionForecaster.Infrastructure.DataSources.Models;

namespace ElectionForecaster.Infrastructure.DataSources.Polling;

/// <summary>
/// Folds an undecided-primary poll's multiple hypothetical-matchup rows (one per pairing the
/// pollster tested, sharing pollster + date) into one model-facing poll before averaging.
///
/// While a primary is undecided every tested matchup counts equally: the effective poll is the
/// mean across them, and the margin spread between them is recorded so the orchestrator can price
/// nominee uncertainty as extra standard error. Once a side's nominee is settled, only matchups
/// featuring that nominee count — a poll whose every pairing stars a candidate who went on to
/// lose their primary is dropped from the model entirely, not blended. This only shapes the
/// model's average; the polls pages display the raw rows exactly as published.
/// </summary>
public static class MatchupBlender
{
    public static List<PollData> Collapse(
        IReadOnlyList<PollData> polls,
        string? demNominee = null,
        string? repNominee = null)
    {
        return polls
            .GroupBy(p => (p.Pollster, Date: p.Date.Date))
            .Select(g => CollapseGroup(g.ToList(), demNominee, repNominee))
            .OfType<PollData>()
            .ToList();
    }

    /// <summary>One pollster-day's matchup rows → the effective poll, or null to exclude it.</summary>
    private static PollData? CollapseGroup(List<PollData> matchups, string? demNominee, string? repNominee)
    {
        // Rows stored before matchups were tracked carry no candidate names. They were the
        // published nominee matchup of their day, so they pass through unjudged (the next
        // parse supersedes them with candidate-tagged rows).
        var tagged = matchups.Where(m => m.DemCandidate is not null || m.RepCandidate is not null).ToList();
        if (tagged.Count > 0)
        {
            // Settled primaries prune the set to matchups the actual nominees appear in. A poll
            // that only tested pairings of since-defeated candidates says nothing about the race
            // as it now stands — exclude it from the model.
            matchups = tagged
                .Where(m => MatchesNominee(m.DemCandidate, demNominee)
                         && MatchesNominee(m.RepCandidate, repNominee))
                .ToList();
            if (matchups.Count == 0) return null;
        }

        if (matchups.Count == 1) return matchups[0];

        // Clone rather than mutate: the source rows may be shared (client cache, display).
        var first = matchups[0];
        return new PollData
        {
            RaceId = first.RaceId,
            Pollster = first.Pollster,
            Date = first.Date,
            SampleSize = first.SampleSize,
            Population = first.Population,
            PollsterRating = first.PollsterRating,
            Methodology = first.Methodology,
            SourceUrl = first.SourceUrl,
            DemPercent = matchups.Average(m => m.DemPercent),
            RepPercent = matchups.Average(m => m.RepPercent),
            MatchupCount = matchups.Count,
            MatchupSpread = matchups.Max(m => m.Margin) - matchups.Min(m => m.Margin),
        };
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
