namespace ElectionForecaster.Infrastructure.Data;

/// <summary>
/// Whether each 2026 statewide race's incumbent is seeking reelection (true = D, false = R,
/// null = open seat), independent of whether the primary has produced a nominee yet — candidate
/// flags are empty pre-primary, which would wrongly drop a running incumbent's prior.
/// Scraped 2026-07-11 from the Wikipedia race-summary tables; regenerate with
/// tools/scrape_statewide_incumbents.py after primaries or retirement news.
/// </summary>
public static class StatewideIncumbents
{
    private static readonly Dictionary<string, bool?> IncumbentIsDemByRace = new()
    {
        ["AK-GOV-2026"] = null,
        ["AL-GOV-2026"] = null,
        ["AR-GOV-2026"] = false,
        ["AZ-GOV-2026"] = true,
        ["CA-GOV-2026"] = null,
        ["CO-GOV-2026"] = null,
        ["CT-GOV-2026"] = true,
        ["FL-GOV-2026"] = null,
        ["GA-GOV-2026"] = null,
        ["HI-GOV-2026"] = true,
        ["IA-GOV-2026"] = null,
        ["ID-GOV-2026"] = false,
        ["IL-GOV-2026"] = true,
        ["KS-GOV-2026"] = null,
        ["MA-GOV-2026"] = true,
        ["MD-GOV-2026"] = true,
        ["ME-GOV-2026"] = null,
        ["MI-GOV-2026"] = null,
        ["MN-GOV-2026"] = null,
        ["NE-GOV-2026"] = false,
        ["NH-GOV-2026"] = false,
        ["NM-GOV-2026"] = null,
        ["NV-GOV-2026"] = false,
        ["NY-GOV-2026"] = true,
        ["OH-GOV-2026"] = null,
        ["OK-GOV-2026"] = null,
        ["OR-GOV-2026"] = true,
        ["PA-GOV-2026"] = true,
        ["RI-GOV-2026"] = true,
        ["SC-GOV-2026"] = null,
        ["SD-GOV-2026"] = false,
        ["TN-GOV-2026"] = null,
        ["TX-GOV-2026"] = false,
        ["VT-GOV-2026"] = false,
        ["WI-GOV-2026"] = null,
        ["WY-GOV-2026"] = null,
        ["AK-SEN-2026"] = false,
        ["AL-SEN-2026"] = null,
        ["AR-SEN-2026"] = false,
        ["CO-SEN-2026"] = true,
        ["DE-SEN-2026"] = true,
        ["GA-SEN-2026"] = true,
        ["IA-SEN-2026"] = null,
        ["ID-SEN-2026"] = false,
        ["IL-SEN-2026"] = null,
        ["KS-SEN-2026"] = false,
        ["KY-SEN-2026"] = null,
        ["LA-SEN-2026"] = null,
        ["MA-SEN-2026"] = true,
        ["ME-SEN-2026"] = false,
        ["MI-SEN-2026"] = null,
        ["MN-SEN-2026"] = null,
        ["MS-SEN-2026"] = false,
        ["MT-SEN-2026"] = null,
        ["NC-SEN-2026"] = null,
        ["NE-SEN-2026"] = false,
        ["NH-SEN-2026"] = null,
        ["NJ-SEN-2026"] = true,
        ["NM-SEN-2026"] = true,
        ["OK-SEN-2026"] = false,
        ["OR-SEN-2026"] = true,
        ["RI-SEN-2026"] = true,
        ["SC-SEN-2026"] = false,
        ["SD-SEN-2026"] = false,
        ["TN-SEN-2026"] = false,
        ["TX-SEN-2026"] = null,
        ["VA-SEN-2026"] = true,
        ["WV-SEN-2026"] = false,
        ["WY-SEN-2026"] = null,
    };

    /// <summary>Incumbent party seeking reelection for a statewide race, or null when the seat
    /// is open (or the race isn't a modeled statewide race, e.g. House).</summary>
    public static bool? GetIncumbentIsDem(string raceId)
        => IncumbentIsDemByRace.TryGetValue(raceId, out var v) ? v : null;
}
