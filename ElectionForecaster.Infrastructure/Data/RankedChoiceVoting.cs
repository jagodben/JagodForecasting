namespace ElectionForecaster.Infrastructure.Data;

/// <summary>
/// Races decided by ranked-choice voting. Alaska runs a top-four RCV general for every federal and
/// state office; Maine uses RCV for its federal races (Senate and House) but not for governor.
///
/// These races are polled as head-to-head matchups (the likely final RCV round), so the two-way
/// model reads them like any other Dem-vs-Rep race. The extra wrinkle RCV adds is uncertainty about
/// which two candidates reach the final round and how lower-choice votes transfer — e.g. in the 2026
/// Alaska at-large House race the independent runs several points closer to the Republican than the
/// Democrat does. We capture that with a wider standard error rather than trying to model transfers.
/// </summary>
public static class RankedChoiceVoting
{
    private static readonly HashSet<string> Races = new(StringComparer.OrdinalIgnoreCase)
    {
        // Alaska — all offices
        "AK-SEN-2026", "AK-GOV-2026", "AK-01-2026",
        // Maine — federal only (the governor's race is not RCV)
        "ME-SEN-2026", "ME-01-2026", "ME-02-2026",
    };

    /// <summary>Extra standard error (margin points) for the RCV final-round / transfer uncertainty.</summary>
    public const double ExtraStandardError = 2.5;

    public static bool IsRankedChoice(string raceId) => Races.Contains(raceId);
}
