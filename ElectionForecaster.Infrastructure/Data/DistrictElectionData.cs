namespace ElectionForecaster.Infrastructure.Data;

/// <summary>
/// District-level partisan data on the lines actually in use for the 2026 elections.
/// Ten states were redrawn mid-decade for 2026 (AL, CA, FL, LA, MO, NC, OH, TN, TX, UT);
/// their PVI values here reflect the NEW lines and they carry no 2024 prior result.
/// Sources: 2025 partisan lean index as compiled (with post-redistricting updates) on Wikipedia's
/// "2026 United States House of Representatives elections" page (scraped 2026-07-09);
/// 2024 House results for un-redrawn states.
/// </summary>
public static class DistrictElectionData
{
    /// <summary>
    /// 2024 House election results by district.
    /// Key: "StateId-DistrictNumber" (e.g., "GA-09")
    /// Value: (RepublicanMargin, RepublicanWon) - positive margin means R won by that many points
    /// </summary>
    public static readonly Dictionary<string, (double Margin, bool RepublicanWon)> Results2024 = new()
    {
        // NOTE: the 10 states redrawn mid-decade for 2026 (AL, CA, FL, LA, MO, NC, OH,
        // TN, TX, UT) are intentionally absent: their 2024 results were earned on lines
        // that no longer exist, so those districts carry no prior result.
        // Values are real 2024 results (top-Dem% minus top-Rep%, negated to R-positive);
        // one-party races carry a +/-45 safe-seat placeholder. Regenerate with
        // tools/scrape_house_results.py against the Wikipedia 2024 House elections page.
        // Alaska
        { "AK-01", (2.4, true) },
        // Arizona
        { "AZ-01", (3.8, true) }, { "AZ-02", (9, true) }, { "AZ-03", (-44.3, false) },
        { "AZ-04", (-7.2, false) }, { "AZ-05", (20.8, true) }, { "AZ-06", (2.5, true) },
        { "AZ-07", (-26.8, false) }, { "AZ-08", (13, true) }, { "AZ-09", (30.6, true) },
        // Arkansas
        { "AR-01", (48.9, true) }, { "AR-02", (17.8, true) }, { "AR-03", (32, true) },
        { "AR-04", (45.8, true) },
        // Colorado
        { "CO-01", (-55, false) }, { "CO-02", (-39.5, false) }, { "CO-03", (5, true) },
        { "CO-04", (11.6, true) }, { "CO-05", (13.8, true) }, { "CO-06", (-20.5, false) },
        { "CO-07", (-14.1, false) }, { "CO-08", (0.8, true) },
        // Connecticut
        { "CT-01", (-28.3, false) }, { "CT-02", (-16, false) }, { "CT-03", (-17.8, false) },
        { "CT-04", (-23.8, false) }, { "CT-05", (-6.8, false) },
        // Delaware
        { "DE-01", (-15.8, false) },
        // Georgia
        { "GA-01", (24, true) }, { "GA-02", (-12.6, false) }, { "GA-03", (32.6, true) },
        { "GA-04", (-51.2, false) }, { "GA-05", (-71.4, false) }, { "GA-06", (-49.4, false) },
        { "GA-07", (29.8, true) }, { "GA-08", (37.8, true) }, { "GA-09", (38, true) },
        { "GA-10", (26.2, true) }, { "GA-11", (34.6, true) }, { "GA-12", (20.6, true) },
        { "GA-13", (-43.6, false) }, { "GA-14", (28.8, true) },
        // Hawaii
        { "HI-01", (-43.6, false) }, { "HI-02", (-36.3, false) },
        // Idaho
        { "ID-01", (45.6, true) }, { "ID-02", (30.4, true) },
        // Illinois
        { "IL-01", (-31.8, false) }, { "IL-02", (-35.2, false) }, { "IL-03", (-34.6, false) },
        { "IL-04", (-40.2, false) }, { "IL-05", (-38, false) }, { "IL-06", (-8.4, false) },
        { "IL-07", (-66.6, false) }, { "IL-08", (-14.2, false) }, { "IL-09", (-36.8, false) },
        { "IL-10", (-20, false) }, { "IL-11", (-11.2, false) }, { "IL-12", (48.4, true) },
        { "IL-13", (-16.2, false) }, { "IL-14", (-10.2, false) }, { "IL-15", (45, true) },
        { "IL-16", (45, true) }, { "IL-17", (-8.8, false) },
        // Indiana
        { "IN-01", (-8.5, false) }, { "IN-02", (28.1, true) }, { "IN-03", (33.6, true) },
        { "IN-04", (33.9, true) }, { "IN-05", (18.6, true) }, { "IN-06", (32.2, true) },
        { "IN-07", (-39.3, false) }, { "IN-08", (38.5, true) }, { "IN-09", (31.7, true) },
        // Iowa
        { "IA-01", (0.2, true) }, { "IA-02", (15.5, true) }, { "IA-03", (3.8, true) },
        { "IA-04", (34.4, true) },
        // Kansas
        { "KS-01", (38.2, true) }, { "KS-02", (18.9, true) }, { "KS-03", (-10.8, false) },
        { "KS-04", (30, true) },
        // Kentucky
        { "KY-01", (49.4, true) }, { "KY-02", (46.2, true) }, { "KY-03", (-24, false) },
        { "KY-04", (45, true) }, { "KY-05", (45, true) }, { "KY-06", (26.8, true) },
        // Maine
        { "ME-01", (-22.3, false) }, { "ME-02", (-0.6, false) },
        // Maryland
        { "MD-01", (22, true) }, { "MD-02", (-18.7, false) }, { "MD-03", (-21.4, false) },
        { "MD-04", (-77.2, false) }, { "MD-05", (-35.8, false) }, { "MD-06", (-6.4, false) },
        { "MD-07", (-63.1, false) }, { "MD-08", (-56.3, false) },
        // Massachusetts
        { "MA-01", (-45, false) }, { "MA-02", (-45, false) }, { "MA-03", (-45, false) },
        { "MA-04", (-45, false) }, { "MA-05", (-45, false) }, { "MA-06", (-45, false) },
        { "MA-07", (-45, false) }, { "MA-08", (-41, false) }, { "MA-09", (-13, false) },
        // Michigan
        { "MI-01", (21.2, true) }, { "MI-02", (33.4, true) }, { "MI-03", (-9.9, false) },
        { "MI-04", (11.7, true) }, { "MI-05", (32.9, true) }, { "MI-06", (-27, false) },
        { "MI-07", (3.7, true) }, { "MI-08", (-6.6, false) }, { "MI-09", (37.3, true) },
        { "MI-10", (6.1, true) }, { "MI-11", (-18.6, false) }, { "MI-12", (-44.3, false) },
        { "MI-13", (-44.1, false) },
        // Minnesota
        { "MN-01", (17, true) }, { "MN-02", (-13.5, false) }, { "MN-03", (-17, false) },
        { "MN-04", (-34.8, false) }, { "MN-05", (-50.4, false) }, { "MN-06", (25, true) },
        { "MN-07", (41, true) }, { "MN-08", (16, true) },
        // Mississippi
        { "MS-01", (39.6, true) }, { "MS-02", (-24, false) }, { "MS-03", (45, true) },
        { "MS-04", (47.9, true) },
        // Montana
        { "MT-01", (7.7, true) }, { "MT-02", (32, true) },
        // Nebraska
        { "NE-01", (20.2, true) }, { "NE-02", (1.8, true) }, { "NE-03", (60.8, true) },
        // Nevada
        { "NV-01", (-7.5, false) }, { "NV-02", (45, true) }, { "NV-03", (-2.8, false) },
        { "NV-04", (-8.1, false) },
        // New Hampshire
        { "NH-01", (-8, false) }, { "NH-02", (-6, false) },
        // New Jersey
        { "NJ-01", (-17.8, false) }, { "NJ-02", (16.9, true) }, { "NJ-03", (-8.6, false) },
        { "NJ-04", (35.7, true) }, { "NJ-05", (-11.3, false) }, { "NJ-06", (-15.8, false) },
        { "NJ-07", (5.4, true) }, { "NJ-08", (-24.8, false) }, { "NJ-09", (-4.9, false) },
        { "NJ-10", (-52.2, false) }, { "NJ-11", (-15.3, false) }, { "NJ-12", (-24.8, false) },
        // New Mexico
        { "NM-01", (-12.8, false) }, { "NM-02", (-4.2, false) }, { "NM-03", (-12.6, false) },
        // New York
        { "NY-01", (10.4, true) }, { "NY-02", (18.4, true) }, { "NY-03", (-3.6, false) },
        { "NY-04", (-2.2, false) }, { "NY-05", (-45.8, false) }, { "NY-06", (-23.1, false) },
        { "NY-07", (-56.2, false) }, { "NY-08", (-50.8, false) }, { "NY-09", (-48.6, false) },
        { "NY-10", (-67.5, false) }, { "NY-11", (28.2, true) }, { "NY-12", (-61, false) },
        { "NY-13", (-67, false) }, { "NY-14", (-38.4, false) }, { "NY-15", (-55.1, false) },
        { "NY-16", (-43.2, false) }, { "NY-17", (6.4, true) }, { "NY-18", (-14.4, false) },
        { "NY-19", (-2.2, false) }, { "NY-20", (-22.2, false) }, { "NY-21", (24, true) },
        { "NY-22", (-9.2, false) }, { "NY-23", (31.6, true) }, { "NY-24", (31.4, true) },
        { "NY-25", (-21.6, false) }, { "NY-26", (-30.4, false) },
        // North Dakota
        { "ND-01", (39, true) },
        // Oklahoma
        { "OK-01", (25.9, true) }, { "OK-02", (52.8, true) }, { "OK-03", (45, true) },
        { "OK-04", (36.9, true) }, { "OK-05", (21.4, true) },
        // Oregon
        { "OR-01", (-40.6, false) }, { "OR-02", (31.2, true) }, { "OR-03", (-42.6, false) },
        { "OR-04", (-7.8, false) }, { "OR-05", (-2.7, false) }, { "OR-06", (-6.8, false) },
        // Pennsylvania
        { "PA-01", (12.8, true) }, { "PA-02", (-42.9, false) }, { "PA-03", (-45, false) },
        { "PA-04", (-18.2, false) }, { "PA-05", (-30.6, false) }, { "PA-06", (-12.4, false) },
        { "PA-07", (1, true) }, { "PA-08", (1.6, true) }, { "PA-09", (41, true) },
        { "PA-10", (1.2, true) }, { "PA-11", (25.8, true) }, { "PA-12", (-12.8, false) },
        { "PA-13", (48.4, true) }, { "PA-14", (33.2, true) }, { "PA-15", (43, true) },
        { "PA-16", (27.2, true) }, { "PA-17", (-7.8, false) },
        // Rhode Island
        { "RI-01", (-31.1, false) }, { "RI-02", (-16.8, false) },
        // South Carolina
        { "SC-01", (16.6, true) }, { "SC-02", (19.4, true) }, { "SC-03", (46.4, true) },
        { "SC-04", (22.6, true) }, { "SC-05", (27.2, true) }, { "SC-06", (-22.8, false) },
        { "SC-07", (30, true) },
        // South Dakota
        { "SD-01", (44, true) },
        // Vermont
        { "VT-01", (-32.6, false) },
        // Virginia
        { "VA-01", (12.8, true) }, { "VA-02", (3.8, true) }, { "VA-03", (-40.2, false) },
        { "VA-04", (-35, false) }, { "VA-05", (15, true) }, { "VA-06", (28.4, true) },
        { "VA-07", (-2.6, false) }, { "VA-08", (-47, false) }, { "VA-09", (45.2, true) },
        { "VA-10", (-4.6, false) }, { "VA-11", (-34, false) },
        // Washington
        { "WA-01", (-26.4, false) }, { "WA-02", (-28, false) }, { "WA-03", (-3.8, false) },
        { "WA-04", (45, true) }, { "WA-05", (21.4, true) }, { "WA-06", (-13.6, false) },
        { "WA-07", (-68.4, false) }, { "WA-08", (-8.2, false) }, { "WA-09", (-45, false) },
        { "WA-10", (-17.4, false) },
        // West Virginia
        { "WV-01", (42.2, true) }, { "WV-02", (41.6, true) },
        // Wisconsin
        { "WI-01", (10.2, true) }, { "WI-02", (-40.2, false) }, { "WI-03", (2.8, true) },
        { "WI-04", (-52.5, false) }, { "WI-05", (29, true) }, { "WI-06", (22.6, true) },
        { "WI-07", (27.2, true) }, { "WI-08", (14.6, true) },
        // Wyoming
        { "WY-01", (47.6, true) },
    };

    /// <summary>
    /// 2025 partisan voting index by congressional district (derived from presidential results), on the 2026 election
    /// lines (mid-decade redraws included — those states are tagged "(2026 lines)").
    /// Positive = Republican lean, Negative = Democratic lean.
    /// Regenerate via the scraper against Wikipedia's 2026 House elections page if lines change.
    /// </summary>
    public static readonly Dictionary<string, double> DistrictPVI = new()
    {
        // Alabama (2026 lines)
        { "AL-01", 17 }, { "AL-02", 7 }, { "AL-03", 23 }, { "AL-04", 33 },
        { "AL-05", 15 }, { "AL-06", 17 }, { "AL-07", -10 },
        // Alaska
        { "AK-01", 6 },
        // Arizona
        { "AZ-01", 1 }, { "AZ-02", 7 }, { "AZ-03", -22 }, { "AZ-04", -4 },
        { "AZ-05", 10 }, { "AZ-06", 0 }, { "AZ-07", -13 }, { "AZ-08", 8 },
        { "AZ-09", 15 },
        // Arkansas
        { "AR-01", 23 }, { "AR-02", 8 }, { "AR-03", 13 }, { "AR-04", 20 },
        // California (2026 lines)
        { "CA-01", -7 }, { "CA-02", -13 }, { "CA-03", -6 }, { "CA-04", -8 },
        { "CA-05", 10 }, { "CA-06", -5 }, { "CA-07", -7 }, { "CA-08", -19 },
        { "CA-09", -8 }, { "CA-10", -18 }, { "CA-11", -36 }, { "CA-12", -39 },
        { "CA-13", -2 }, { "CA-14", -19 }, { "CA-15", -26 }, { "CA-16", -25 },
        { "CA-17", -21 }, { "CA-18", -16 }, { "CA-19", -18 }, { "CA-20", 16 },
        { "CA-21", -5 }, { "CA-22", -1 }, { "CA-23", 9 }, { "CA-24", -13 },
        { "CA-25", -4 }, { "CA-26", -9 }, { "CA-27", -6 }, { "CA-28", -14 },
        { "CA-29", -19 }, { "CA-30", -21 }, { "CA-31", -8 }, { "CA-32", -14 },
        { "CA-33", -7 }, { "CA-34", -28 }, { "CA-35", -6 }, { "CA-36", -21 },
        { "CA-37", -33 }, { "CA-38", -8 }, { "CA-39", -7 }, { "CA-40", 6 },
        { "CA-41", -9 }, { "CA-42", -8 }, { "CA-43", -27 }, { "CA-44", -20 },
        { "CA-45", -3 }, { "CA-46", -10 }, { "CA-47", -6 }, { "CA-48", -2 },
        { "CA-49", -7 }, { "CA-50", -10 }, { "CA-51", -10 }, { "CA-52", -11 },
        // Colorado
        { "CO-01", -29 }, { "CO-02", -20 }, { "CO-03", 5 }, { "CO-04", 9 },
        { "CO-05", 5 }, { "CO-06", -11 }, { "CO-07", -8 }, { "CO-08", 0 },
        // Connecticut
        { "CT-01", -12 }, { "CT-02", -4 }, { "CT-03", -8 }, { "CT-04", -13 },
        { "CT-05", -3 },
        // Delaware
        { "DE-01", -8 },
        // Florida (2026 lines)
        { "FL-01", 18 }, { "FL-02", 8 }, { "FL-03", 10 }, { "FL-04", 5 },
        { "FL-05", 10 }, { "FL-06", 14 }, { "FL-07", 5 }, { "FL-08", 8 },
        { "FL-09", 8 }, { "FL-10", -13 }, { "FL-11", 7 }, { "FL-12", 7 },
        { "FL-13", 6 }, { "FL-14", 4 }, { "FL-15", 9 }, { "FL-16", 6 },
        { "FL-17", 10 }, { "FL-18", 8 }, { "FL-19", 14 }, { "FL-20", -20 },
        { "FL-21", 7 }, { "FL-22", 4 }, { "FL-23", -9 }, { "FL-24", -22 },
        { "FL-25", 3 }, { "FL-26", 7 }, { "FL-27", 6 }, { "FL-28", 10 },
        // Georgia
        { "GA-01", 8 }, { "GA-02", -4 }, { "GA-03", 15 }, { "GA-04", -27 },
        { "GA-05", -36 }, { "GA-06", -25 }, { "GA-07", 11 }, { "GA-08", 15 },
        { "GA-09", 17 }, { "GA-10", 11 }, { "GA-11", 12 }, { "GA-12", 7 },
        { "GA-13", -21 }, { "GA-14", 19 },
        // Hawaii
        { "HI-01", -13 }, { "HI-02", -12 },
        // Idaho
        { "ID-01", 22 }, { "ID-02", 13 },
        // Illinois
        { "IL-01", -18 }, { "IL-02", -18 }, { "IL-03", -17 }, { "IL-04", -17 },
        { "IL-05", -19 }, { "IL-06", -3 }, { "IL-07", -34 }, { "IL-08", -5 },
        { "IL-09", -19 }, { "IL-10", -12 }, { "IL-11", -6 }, { "IL-12", 22 },
        { "IL-13", -5 }, { "IL-14", -3 }, { "IL-15", 20 }, { "IL-16", 11 },
        { "IL-17", -3 },
        // Indiana
        { "IN-01", -1 }, { "IN-02", 13 }, { "IN-03", 16 }, { "IN-04", 15 },
        { "IN-05", 8 }, { "IN-06", 16 }, { "IN-07", -21 }, { "IN-08", 18 },
        { "IN-09", 15 },
        // Iowa
        { "IA-01", 4 }, { "IA-02", 4 }, { "IA-03", 2 }, { "IA-04", 15 },
        // Kansas
        { "KS-01", 16 }, { "KS-02", 10 }, { "KS-03", -2 }, { "KS-04", 12 },
        // Kentucky
        { "KY-01", 23 }, { "KY-02", 20 }, { "KY-03", -10 }, { "KY-04", 18 },
        { "KY-05", 32 }, { "KY-06", 7 },
        // Louisiana (2026 lines)
        { "LA-01", 20 }, { "LA-02", -25 }, { "LA-03", 18 }, { "LA-04", 17 },
        { "LA-05", 18 }, { "LA-06", 16 },
        // Maine
        { "ME-01", -11 }, { "ME-02", 4 },
        // Maryland
        { "MD-01", 8 }, { "MD-02", -10 }, { "MD-03", -12 }, { "MD-04", -39 },
        { "MD-05", -17 }, { "MD-06", -3 }, { "MD-07", -31 }, { "MD-08", -30 },
        // Massachusetts
        { "MA-01", -8 }, { "MA-02", -13 }, { "MA-03", -11 }, { "MA-04", -11 },
        { "MA-05", -24 }, { "MA-06", -11 }, { "MA-07", -34 }, { "MA-08", -15 },
        { "MA-09", -6 },
        // Michigan
        { "MI-01", 11 }, { "MI-02", 15 }, { "MI-03", -4 }, { "MI-04", 3 },
        { "MI-05", 13 }, { "MI-06", -12 }, { "MI-07", 0 }, { "MI-08", 1 },
        { "MI-09", 16 }, { "MI-10", 3 }, { "MI-11", -9 }, { "MI-12", -21 },
        { "MI-13", -22 },
        // Minnesota
        { "MN-01", 6 }, { "MN-02", -3 }, { "MN-03", -11 }, { "MN-04", -18 },
        { "MN-05", -32 }, { "MN-06", 10 }, { "MN-07", 18 }, { "MN-08", 7 },
        // Mississippi
        { "MS-01", 18 }, { "MS-02", -11 }, { "MS-03", 14 }, { "MS-04", 21 },
        // Missouri (2026 lines)
        { "MO-01", -29 }, { "MO-02", 6 }, { "MO-03", 10 }, { "MO-04", 10 },
        { "MO-05", 9 }, { "MO-06", 13 }, { "MO-07", 21 }, { "MO-08", 27 },
        // Montana
        { "MT-01", 5 }, { "MT-02", 15 },
        // Nebraska
        { "NE-01", 6 }, { "NE-02", -3 }, { "NE-03", 27 },
        // Nevada
        { "NV-01", -2 }, { "NV-02", 7 }, { "NV-03", -1 }, { "NV-04", -2 },
        // New Hampshire
        { "NH-01", -2 }, { "NH-02", -2 },
        // New Jersey
        { "NJ-01", -10 }, { "NJ-02", 5 }, { "NJ-03", -5 }, { "NJ-04", 14 },
        { "NJ-05", -2 }, { "NJ-06", -5 }, { "NJ-07", 0 }, { "NJ-08", -15 },
        { "NJ-09", -2 }, { "NJ-10", -27 }, { "NJ-11", -5 }, { "NJ-12", -13 },
        // New Mexico
        { "NM-01", -7 }, { "NM-02", 0 }, { "NM-03", -3 },
        // New York
        { "NY-01", 4 }, { "NY-02", 6 }, { "NY-03", 0 }, { "NY-04", -2 },
        { "NY-05", -24 }, { "NY-06", -6 }, { "NY-07", -25 }, { "NY-08", -24 },
        { "NY-09", -22 }, { "NY-10", -32 }, { "NY-11", 10 }, { "NY-12", -33 },
        { "NY-13", -32 }, { "NY-14", -19 }, { "NY-15", -27 }, { "NY-16", -18 },
        { "NY-17", -1 }, { "NY-18", -2 }, { "NY-19", -1 }, { "NY-20", -8 },
        { "NY-21", 10 }, { "NY-22", -4 }, { "NY-23", 10 }, { "NY-24", 11 },
        { "NY-25", -10 }, { "NY-26", -11 },
        // North Carolina (2026 lines)
        { "NC-01", 5 }, { "NC-02", -17 }, { "NC-03", 6 }, { "NC-04", -23 },
        { "NC-05", 9 }, { "NC-06", 9 }, { "NC-07", 7 }, { "NC-08", 10 },
        { "NC-09", 8 }, { "NC-10", 9 }, { "NC-11", 5 }, { "NC-12", -24 },
        { "NC-13", 8 }, { "NC-14", 8 },
        // North Dakota
        { "ND-01", 18 },
        // Ohio (2026 lines)
        { "OH-01", 1 }, { "OH-02", 21 }, { "OH-03", -21 }, { "OH-04", 21 },
        { "OH-05", 12 }, { "OH-06", 17 }, { "OH-07", 5 }, { "OH-08", 8 },
        { "OH-09", 5 }, { "OH-10", 4 }, { "OH-11", -28 }, { "OH-12", 15 },
        { "OH-13", -2 }, { "OH-14", 10 }, { "OH-15", 5 },
        // Oklahoma
        { "OK-01", 11 }, { "OK-02", 28 }, { "OK-03", 23 }, { "OK-04", 17 },
        { "OK-05", 9 },
        // Oregon
        { "OR-01", -20 }, { "OR-02", 14 }, { "OR-03", -24 }, { "OR-04", -6 },
        { "OR-05", -4 }, { "OR-06", -6 },
        // Pennsylvania
        { "PA-01", -1 }, { "PA-02", -19 }, { "PA-03", -40 }, { "PA-04", -8 },
        { "PA-05", -15 }, { "PA-06", -6 }, { "PA-07", 1 }, { "PA-08", 4 },
        { "PA-09", 19 }, { "PA-10", 3 }, { "PA-11", 11 }, { "PA-12", -10 },
        { "PA-13", 23 }, { "PA-14", 17 }, { "PA-15", 19 }, { "PA-16", 11 },
        { "PA-17", -3 },
        // Rhode Island
        { "RI-01", -12 }, { "RI-02", -4 },
        // South Carolina
        { "SC-01", 6 }, { "SC-02", 7 }, { "SC-03", 21 }, { "SC-04", 11 },
        { "SC-05", 11 }, { "SC-06", -13 }, { "SC-07", 12 },
        // South Dakota
        { "SD-01", 15 },
        // Tennessee (2026 lines)
        { "TN-01", 29 }, { "TN-02", 17 }, { "TN-03", 18 }, { "TN-04", 11 },
        { "TN-05", 10 }, { "TN-06", 13 }, { "TN-07", 11 }, { "TN-08", 10 },
        { "TN-09", 9 },
        // Texas (2026 lines)
        { "TX-01", 24 }, { "TX-02", 11 }, { "TX-03", 11 }, { "TX-04", 12 },
        { "TX-05", 10 }, { "TX-06", 11 }, { "TX-07", -13 }, { "TX-08", 13 },
        { "TX-09", 9 }, { "TX-10", 10 }, { "TX-11", 17 }, { "TX-12", 11 },
        { "TX-13", 23 }, { "TX-14", 12 }, { "TX-15", 7 }, { "TX-16", -11 },
        { "TX-17", 10 }, { "TX-18", -29 }, { "TX-19", 25 }, { "TX-20", -16 },
        { "TX-21", 10 }, { "TX-22", 11 }, { "TX-23", 7 }, { "TX-24", 8 },
        { "TX-25", 11 }, { "TX-26", 11 }, { "TX-27", 10 }, { "TX-28", 3 },
        { "TX-29", -17 }, { "TX-30", -25 }, { "TX-31", 11 }, { "TX-32", 8 },
        { "TX-33", -18 }, { "TX-34", 3 }, { "TX-35", 4 }, { "TX-36", 12 },
        { "TX-37", -30 }, { "TX-38", 10 },
        // Utah (2026 lines)
        { "UT-01", -12 }, { "UT-02", 15 }, { "UT-03", 21 }, { "UT-04", 17 },
        // Vermont
        { "VT-01", -17 },
        // Virginia
        { "VA-01", 3 }, { "VA-02", 0 }, { "VA-03", -18 }, { "VA-04", -17 },
        { "VA-05", 6 }, { "VA-06", 12 }, { "VA-07", -2 }, { "VA-08", -26 },
        { "VA-09", 22 }, { "VA-10", -6 }, { "VA-11", -18 },
        // Washington
        { "WA-01", -15 }, { "WA-02", -12 }, { "WA-03", 2 }, { "WA-04", 10 },
        { "WA-05", 5 }, { "WA-06", -10 }, { "WA-07", -39 }, { "WA-08", -3 },
        { "WA-09", -22 }, { "WA-10", -9 },
        // West Virginia
        { "WV-01", 22 }, { "WV-02", 20 },
        // Wisconsin
        { "WI-01", 2 }, { "WI-02", -21 }, { "WI-03", 3 }, { "WI-04", -26 },
        { "WI-05", 11 }, { "WI-06", 8 }, { "WI-07", 11 }, { "WI-08", 8 },
        // Wyoming
        { "WY-01", 23 },
    };

    /// <summary>
    /// Get the 2024 election result for a district.
    /// </summary>
    public static (double Margin, bool RepublicanWon)? GetResult2024(string stateId, int districtNumber)
    {
        var key = $"{stateId}-{districtNumber:D2}";
        return Results2024.TryGetValue(key, out var result) ? result : null;
    }

    /// <summary>
    /// Get the partisan lean index for a district. Positive = R lean, Negative = D lean.
    /// </summary>
    public static double GetDistrictPVI(string stateId, int districtNumber)
    {
        var key = $"{stateId}-{districtNumber:D2}";
        return DistrictPVI.TryGetValue(key, out var pvi) ? pvi : 0;
    }
}
