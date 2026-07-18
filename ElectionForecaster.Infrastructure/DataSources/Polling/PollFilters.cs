namespace ElectionForecaster.Infrastructure.DataSources.Polling;

/// <summary>
/// Decides whether a parsed D/R pair is a usable head-to-head reading. Multi-candidate field
/// polls (e.g. Alaska's ranked-choice tables) put a fragmented field in those two columns, and
/// rescaling D41/R19 to a two-party margin fabricates a D+37 blowout.
/// </summary>
public static class PollFilters
{
    /// <summary>
    /// True when D+R plausibly describes a two-way race. Calibrated against live 2026 data:
    /// genuine high-undecided head-to-heads sum 66+ with both nominees at 24%+ (MT 66-70,
    /// VT 69, TN 78), while field polls sum ≤61 with one side under 20 (AK 60, RI 60, SD 61).
    /// </summary>
    public static bool IsUsableTwoWay(double demPct, double repPct)
    {
        var sum = demPct + repPct;
        if (sum < 65) return false;
        // A borderline sum is still a field poll when one "nominee" polls in the teens —
        // that column is one candidate of a fragmented side, not a party's standard-bearer.
        if (sum < 75 && Math.Min(demPct, repPct) < 20) return false;
        return true;
    }
}
