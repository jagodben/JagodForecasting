namespace ElectionForecaster.Infrastructure.DataSources.Polling;

/// <summary>
/// Coarse quality tiers for common U.S. pollsters, used to weight polls in the average. 538's
/// pollster ratings were retired, so this is an approximate, reputational tiering (methodology,
/// transparency, and track record) rather than any single published grade — not a precise score.
/// Grades are the same letters <see cref="Models.PollData.GetPollsterRatingMultiplier"/> understands.
/// </summary>
public static class PollsterRatings
{
    // Key = lowercase substring matched against the pollster name (so sponsor suffixes like
    // "Emerson College/The Hill" still resolve); value = letter grade. First match wins, so more
    // specific keys should precede more general ones.
    private static readonly (string Key, string Grade)[] Ratings =
    {
        // Gold standard
        ("siena", "A"), ("new york times", "A"), ("marquette", "A"), ("monmouth", "A"),
        ("selzer", "A-"), ("marist", "A-"), ("suffolk", "A-"), ("survey usa", "A-"),
        ("surveyusa", "A-"), ("abc news", "A-"), ("washington post", "A-"), ("emerson", "A-"),
        // Solid
        ("quinnipiac", "B+"), ("fox news", "B+"), ("cnn", "B+"), ("yougov", "B+"),
        ("echelon", "B+"), ("atlasintel", "B"), ("morning consult", "B"), ("beacon", "B"),
        ("shaw", "B"),
        // Partisan-leaning but methodologically standard
        ("data for progress", "B-"), ("public policy polling", "B-"), ("ppp", "B-"),
        // House-effect-heavy / lower transparency
        ("cygnal", "C+"), ("insideradvantage", "C"), ("co/efficient", "C"), ("coefficient", "C"),
        ("rasmussen", "C"), ("trafalgar", "C"), ("patriot polling", "C-"), ("rmg research", "C+"),
    };

    /// <summary>The tier for a pollster, or null if unknown (callers then use a neutral default).</summary>
    public static string? GetRating(string pollster)
    {
        if (string.IsNullOrWhiteSpace(pollster)) return null;
        var name = pollster.ToLowerInvariant();
        foreach (var (key, grade) in Ratings)
            if (name.Contains(key)) return grade;
        return null;
    }
}
