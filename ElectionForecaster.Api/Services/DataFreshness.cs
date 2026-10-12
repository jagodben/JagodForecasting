namespace ElectionForecaster.Api.Services;

/// <summary>
/// Which daily feeds have stopped updating. Every scraper falls back to its last stored value when
/// a fetch fails, which keeps the site up but hides the failure: a renamed Wikipedia section once
/// froze the generic ballot for 25 days, and a split California page froze 52 districts' candidates
/// and polls, all without an error. /health reports these, and the nightly workflow fails on them.
/// </summary>
public static class DataFreshness
{
    public static List<string> StaleSources(
        DateTime now,
        DateTime? latestSnapshot,
        DateTime? latestGenericBallot,
        IEnumerable<(string RaceId, DateTime UpdatedAt)> nomineeRefreshes)
    {
        var stale = new List<string>();
        var today = now.Date;

        // The snapshot is written each morning, so yesterday's is fine until 8 AM Eastern.
        if (latestSnapshot is null || latestSnapshot.Value.Date < today.AddDays(-1))
            stale.Add($"forecast snapshot (latest {Day(latestSnapshot)})");

        // A ballot point is stored on every day the aggregate table parses.
        if (latestGenericBallot is null || latestGenericBallot.Value.Date < today.AddDays(-2))
            stale.Add($"generic ballot (latest {Day(latestGenericBallot)})");

        // Judged per Wikipedia page, not per race: a race whose infobox simply lists no nominee yet
        // never refreshes, but a page where nothing refreshed means the page itself stopped parsing.
        foreach (var page in nomineeRefreshes
                     .GroupBy(r => CandidatePage(r.RaceId))
                     .Where(g => g.Max(r => r.UpdatedAt) < now.AddDays(-3))
                     .OrderBy(g => g.Key))
        {
            stale.Add($"candidates: {page.Key} (last refreshed {Day(page.Max(r => r.UpdatedAt))})");
        }

        return stale;
    }

    /// <summary>House races share their state's page; statewide races each have their own.</summary>
    private static string CandidatePage(string raceId)
    {
        var parts = raceId.Split('-');
        return parts.Length >= 3 && int.TryParse(parts[1], out _) ? $"{parts[0]} House" : raceId;
    }

    private static string Day(DateTime? date) => date?.ToString("yyyy-MM-dd") ?? "never";
}
