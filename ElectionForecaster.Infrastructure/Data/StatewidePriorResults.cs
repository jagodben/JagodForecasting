namespace ElectionForecaster.Infrastructure.Data;

/// <summary>
/// The last comparable result for each 2026 Senate/Governor seat, as a Democratic margin in
/// points. Lets the fundamentals see a seat's demonstrated lean beyond PVI (crossover
/// incumbents, safe-seat blowouts). Prior race = the last time the seat was on the ballot
/// (2020 for Class-2 Senate, the most recent special for FL/OH/NE, 2022/2024 for governors).
/// Values rounded, capped at ±45; unopposed races use a placeholder. The model shrinks these
/// toward PVI, so approximate magnitudes are fine.
/// </summary>
public static class StatewidePriorResults
{
    // raceId -> prior Democratic margin (points).
    private static readonly Dictionary<string, double> PriorDemMargin = new()
    {
        // ---- Senate (2020, unless noted) ----
        ["AK-SEN-2026"] = -13,   ["AL-SEN-2026"] = -20,   ["AR-SEN-2026"] = -35, // AR: Cotton unopposed by a Dem
        ["CO-SEN-2026"] = 9,     ["DE-SEN-2026"] = 22,    ["FL-SEN-2026"] = -16, // FL: 2022 special seat (Rubio)
        ["GA-SEN-2026"] = 1,     ["IA-SEN-2026"] = -7,    ["ID-SEN-2026"] = -29,
        ["IL-SEN-2026"] = 16,    ["KS-SEN-2026"] = -11,   ["KY-SEN-2026"] = -20,
        ["LA-SEN-2026"] = -35,   ["MA-SEN-2026"] = 33,    ["ME-SEN-2026"] = -9,  // ME: Collins crossover incumbent
        ["MI-SEN-2026"] = 2,     ["MN-SEN-2026"] = 5,     ["MS-SEN-2026"] = -10,
        ["MT-SEN-2026"] = -10,   ["NC-SEN-2026"] = -2,    ["NE-SEN-2026"] = -25, // NE: 2024 special (Ricketts)
        ["NH-SEN-2026"] = 16,    ["NJ-SEN-2026"] = 16,    ["NM-SEN-2026"] = 6,
        ["OH-SEN-2026"] = -6,    ["OK-SEN-2026"] = -26,   ["OR-SEN-2026"] = 18,  // OH: 2022 special seat (Vance); OK: 2022 special (Mullin)
        ["RI-SEN-2026"] = 33,    ["SC-SEN-2026"] = -10,   ["SD-SEN-2026"] = -31,
        ["TN-SEN-2026"] = -27,   ["TX-SEN-2026"] = -10,   ["VA-SEN-2026"] = 12,
        ["WV-SEN-2026"] = -35,   ["WY-SEN-2026"] = -45,

        // ---- Governor (2022, unless noted) ----
        ["AK-GOV-2026"] = -20,   ["AL-GOV-2026"] = -38,   ["AR-GOV-2026"] = -28,
        ["AZ-GOV-2026"] = 1,     ["CA-GOV-2026"] = 18,    ["CO-GOV-2026"] = 19,
        ["CT-GOV-2026"] = 13,    ["FL-GOV-2026"] = -19,   ["GA-GOV-2026"] = -8,
        ["HI-GOV-2026"] = 27,    ["IA-GOV-2026"] = -18,   ["ID-GOV-2026"] = -40,
        ["IL-GOV-2026"] = 12,    ["KS-GOV-2026"] = 2,     ["MA-GOV-2026"] = 29,
        ["MD-GOV-2026"] = 32,    ["ME-GOV-2026"] = 13,    ["MI-GOV-2026"] = 11,
        ["MN-GOV-2026"] = 8,     ["NE-GOV-2026"] = -24,   ["NH-GOV-2026"] = -9,  // NH: 2024 (Ayotte)
        ["NM-GOV-2026"] = 6,     ["NV-GOV-2026"] = -2,    ["NY-GOV-2026"] = 6,
        ["OH-GOV-2026"] = -25,   ["OK-GOV-2026"] = -14,   ["OR-GOV-2026"] = 3,
        ["PA-GOV-2026"] = 15,    ["RI-GOV-2026"] = 19,    ["SC-GOV-2026"] = -16,
        ["SD-GOV-2026"] = -27,   ["TN-GOV-2026"] = -32,   ["TX-GOV-2026"] = -11,
        ["VT-GOV-2026"] = -49,   ["WI-GOV-2026"] = 3,     ["WY-GOV-2026"] = -45,
    };

    /// <summary>The prior Dem margin for a race, or null if we have no prior on file (e.g. House).</summary>
    public static double? GetPriorMargin(string raceId)
        => PriorDemMargin.TryGetValue(raceId, out var m) ? m : null;
}
