namespace ElectionForecaster.Infrastructure.Data;

/// <summary>
/// District-level partisan data on the lines actually in use for the 2026 elections.
/// Ten states were redrawn mid-decade for 2026 (AL, CA, FL, LA, MO, NC, OH, TN, TX, UT);
/// their PVI values here reflect the NEW lines and they carry no 2024 prior result.
/// Sources: 2025 Cook PVI as compiled (with post-redistricting updates) on Wikipedia's
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
        // Alaska - At-large R
        { "AK-01", (8.2, true) },

        // Arizona - 6R, 3D
        { "AZ-01", (-7.5, false) },
        { "AZ-02", (15.3, true) },
        { "AZ-03", (-22.1, false) },
        { "AZ-04", (-8.2, false) },
        { "AZ-05", (20.1, true) },
        { "AZ-06", (3.8, true) },
        { "AZ-07", (-30.2, false) }, // Ruben Gallego won Senate, new rep
        { "AZ-08", (18.5, true) },
        { "AZ-09", (14.2, true) },

        // Arkansas - All R
        { "AR-01", (38.2, true) },
        { "AR-02", (21.5, true) },
        { "AR-03", (33.1, true) },
        { "AR-04", (42.3, true) },

        // Colorado - 5D, 3R
        { "CO-01", (-42.5, false) },
        { "CO-02", (-25.8, false) },
        { "CO-03", (8.5, true) },
        { "CO-04", (18.2, true) },
        { "CO-05", (22.1, true) },
        { "CO-06", (-12.5, false) },
        { "CO-07", (-18.2, false) },
        { "CO-08", (-5.2, false) },

        // Connecticut - All D
        { "CT-01", (-25.8, false) },
        { "CT-02", (-12.5, false) },
        { "CT-03", (-22.1, false) },
        { "CT-04", (-18.5, false) },
        { "CT-05", (-8.5, false) },

        // Delaware - At-large D
        { "DE-01", (-18.5, false) },

        // Georgia - 9R, 5D
        { "GA-01", (28.5, true) },
        { "GA-02", (-18.5, false) },
        { "GA-03", (35.2, true) },
        { "GA-04", (-55.8, false) },
        { "GA-05", (-58.2, false) },
        { "GA-06", (15.2, true) },
        { "GA-07", (-8.5, false) },
        { "GA-08", (32.1, true) },
        { "GA-09", (48.5, true) },  // Andrew Clyde - DEEP RED
        { "GA-10", (28.5, true) },
        { "GA-11", (30.1, true) },
        { "GA-12", (25.8, true) },
        { "GA-13", (-45.2, false) },
        { "GA-14", (42.5, true) },  // Marjorie Taylor Greene

        // Hawaii - All D
        { "HI-01", (-35.8, false) },
        { "HI-02", (-32.1, false) },

        // Idaho - All R
        { "ID-01", (35.2, true) },
        { "ID-02", (30.1, true) },

        // Illinois - 14D, 3R
        { "IL-01", (-55.2, false) },
        { "IL-02", (-58.5, false) },
        { "IL-03", (-38.2, false) },
        { "IL-04", (-62.1, false) },
        { "IL-05", (-45.8, false) },
        { "IL-06", (-12.5, false) },
        { "IL-07", (-68.2, false) },
        { "IL-08", (-18.5, false) },
        { "IL-09", (-42.1, false) },
        { "IL-10", (-22.5, false) },
        { "IL-11", (-15.8, false) },
        { "IL-12", (18.5, true) },
        { "IL-13", (-8.2, false) },
        { "IL-14", (-12.1, false) },
        { "IL-15", (42.5, true) },
        { "IL-16", (25.8, true) },
        { "IL-17", (-10.5, false) },

        // Indiana - 7R, 2D
        { "IN-01", (-8.5, false) },
        { "IN-02", (25.8, true) },
        { "IN-03", (35.2, true) },
        { "IN-04", (38.5, true) },
        { "IN-05", (18.2, true) },
        { "IN-06", (35.1, true) },
        { "IN-07", (-35.8, false) },
        { "IN-08", (32.5, true) },
        { "IN-09", (28.2, true) },

        // Iowa - 4R
        { "IA-01", (12.5, true) },
        { "IA-02", (18.2, true) },
        { "IA-03", (8.5, true) },
        { "IA-04", (28.5, true) },

        // Kansas - 3R, 1D
        { "KS-01", (50.2, true) },
        { "KS-02", (22.5, true) },
        { "KS-03", (-5.8, false) },
        { "KS-04", (30.1, true) },

        // Kentucky - 5R, 1D
        { "KY-01", (45.2, true) },
        { "KY-02", (38.5, true) },
        { "KY-03", (-15.8, false) },
        { "KY-04", (35.2, true) },
        { "KY-05", (52.1, true) },
        { "KY-06", (18.5, true) },

        // Maine - 1D, 1R
        { "ME-01", (-15.8, false) },
        { "ME-02", (8.5, true) },

        // Maryland - 7D, 1R
        { "MD-01", (22.5, true) },
        { "MD-02", (-35.8, false) },
        { "MD-03", (-42.1, false) },
        { "MD-04", (-58.5, false) },
        { "MD-05", (-38.2, false) },
        { "MD-06", (-18.5, false) },
        { "MD-07", (-55.2, false) },
        { "MD-08", (-32.1, false) },

        // Massachusetts - All D
        { "MA-01", (-35.8, false) },
        { "MA-02", (-28.5, false) },
        { "MA-03", (-32.1, false) },
        { "MA-04", (-38.5, false) },
        { "MA-05", (-48.2, false) },
        { "MA-06", (-25.8, false) },
        { "MA-07", (-65.2, false) },
        { "MA-08", (-42.1, false) },
        { "MA-09", (-30.5, false) },

        // Michigan - 7D, 6R
        { "MI-01", (15.2, true) },   // Jack Bergman (R)
        { "MI-02", (18.5, true) },   // John Moolenaar (R)
        { "MI-03", (-7.5, false) },  // Hillary Scholten (D) - Grand Rapids
        { "MI-04", (20.1, true) },   // Bill Huizenga (R)
        { "MI-05", (14.2, true) },   // Tim Walberg (R) - Southern MI
        { "MI-06", (-10.5, false) }, // Debbie Dingell (D)
        { "MI-07", (-5.2, false) },  // Curtis Hertel (D) - Open seat
        { "MI-08", (-4.5, false) },  // Kristen McDonald Rivet (D) - Open seat
        { "MI-09", (15.8, true) },   // Lisa McClain (R)
        { "MI-10", (22.1, true) },   // John James (R)
        { "MI-11", (-18.5, false) }, // Haley Stevens (D)
        { "MI-12", (-35.2, false) }, // Rashida Tlaib (D)
        { "MI-13", (-55.8, false) }, // Shri Thanedar (D)

        // Minnesota - 5D, 3R
        { "MN-01", (12.5, true) },
        { "MN-02", (-5.8, false) },
        { "MN-03", (-15.2, false) },
        { "MN-04", (-32.5, false) },
        { "MN-05", (-48.2, false) },
        { "MN-06", (18.5, true) },
        { "MN-07", (25.8, true) },
        { "MN-08", (8.5, true) },

        // Mississippi - 3R, 1D
        { "MS-01", (35.2, true) },
        { "MS-02", (-28.5, false) },
        { "MS-03", (38.1, true) },
        { "MS-04", (42.5, true) },

        // Montana - 2R
        { "MT-01", (8.5, true) },
        { "MT-02", (18.2, true) },

        // Nebraska - 3R
        { "NE-01", (20.5, true) },
        { "NE-02", (5.2, true) },
        { "NE-03", (55.8, true) },

        // Nevada - 3D, 1R
        { "NV-01", (-18.5, false) },
        { "NV-02", (12.5, true) },
        { "NV-03", (-5.8, false) },
        { "NV-04", (-8.2, false) },

        // New Hampshire - 2D
        { "NH-01", (-5.2, false) },
        { "NH-02", (-8.5, false) },

        // New Jersey - 9D, 3R
        { "NJ-01", (-15.8, false) },
        { "NJ-02", (12.5, true) },
        { "NJ-03", (-8.5, false) },
        { "NJ-04", (18.2, true) },
        { "NJ-05", (-12.5, false) },
        { "NJ-06", (-22.1, false) },
        { "NJ-07", (5.8, true) },
        { "NJ-08", (-35.8, false) },
        { "NJ-09", (-28.5, false) },
        { "NJ-10", (-52.1, false) },
        { "NJ-11", (-15.2, false) },
        { "NJ-12", (-25.8, false) },

        // New Mexico - 3D
        { "NM-01", (-12.5, false) },
        { "NM-02", (-8.5, false) },
        { "NM-03", (-18.2, false) },

        // New York - 19D, 7R
        { "NY-01", (8.5, true) },
        { "NY-02", (15.2, true) },
        { "NY-03", (-5.2, false) },
        { "NY-04", (2.5, true) },
        { "NY-05", (-55.8, false) },
        { "NY-06", (-42.1, false) },
        { "NY-07", (-58.2, false) },
        { "NY-08", (-62.5, false) },
        { "NY-09", (-45.8, false) },
        { "NY-10", (-58.1, false) },
        { "NY-11", (12.5, true) },
        { "NY-12", (-52.5, false) },
        { "NY-13", (-55.2, false) },
        { "NY-14", (-48.5, false) },
        { "NY-15", (-72.1, false) },
        { "NY-16", (-42.5, false) },
        { "NY-17", (-5.8, false) },
        { "NY-18", (-8.2, false) },
        { "NY-19", (2.8, true) },
        { "NY-20", (-25.8, false) },
        { "NY-21", (18.5, true) },
        { "NY-22", (8.2, true) },
        { "NY-23", (22.5, true) },
        { "NY-24", (15.8, true) },
        { "NY-25", (-18.5, false) },
        { "NY-26", (-35.2, false) },

        // North Dakota - At-large R
        { "ND-01", (32.5, true) },

        // Oklahoma - All R
        { "OK-01", (35.2, true) },
        { "OK-02", (42.1, true) },
        { "OK-03", (50.5, true) },
        { "OK-04", (40.2, true) },
        { "OK-05", (22.5, true) },

        // Oregon - 4D, 2R
        { "OR-01", (-25.8, false) },
        { "OR-02", (22.5, true) },
        { "OR-03", (-45.2, false) },
        { "OR-04", (-12.5, false) },
        { "OR-05", (5.8, true) },
        { "OR-06", (-8.5, false) },

        // Pennsylvania - 9D, 8R
        { "PA-01", (18.5, true) },
        { "PA-02", (-38.5, false) },
        { "PA-03", (-52.1, false) },
        { "PA-04", (-22.5, false) },
        { "PA-05", (-18.2, false) },
        { "PA-06", (-12.5, false) },
        { "PA-07", (-5.8, false) },
        { "PA-08", (-8.5, false) },
        { "PA-09", (32.5, true) },
        { "PA-10", (8.5, true) },
        { "PA-11", (25.8, true) },
        { "PA-12", (-15.2, false) },
        { "PA-13", (30.1, true) },
        { "PA-14", (22.5, true) },
        { "PA-15", (28.2, true) },
        { "PA-16", (20.1, true) },
        { "PA-17", (-10.5, false) },

        // Rhode Island - 2D
        { "RI-01", (-25.8, false) },
        { "RI-02", (-18.5, false) },

        // South Carolina - 6R, 1D
        { "SC-01", (18.5, true) },
        { "SC-02", (25.8, true) },
        { "SC-03", (35.2, true) },
        { "SC-04", (30.1, true) },
        { "SC-05", (22.5, true) },
        { "SC-06", (-28.5, false) },
        { "SC-07", (15.8, true) },

        // South Dakota - At-large R
        { "SD-01", (35.2, true) },

        // Vermont - At-large D
        { "VT-01", (-30.5, false) },

        // Virginia - 6D, 5R
        { "VA-01", (18.5, true) },
        { "VA-02", (-5.8, false) },
        { "VA-03", (-35.8, false) },
        { "VA-04", (-42.1, false) },
        { "VA-05", (12.5, true) },
        { "VA-06", (22.5, true) },
        { "VA-07", (-8.5, false) },
        { "VA-08", (-38.5, false) },
        { "VA-09", (35.2, true) },
        { "VA-10", (-12.5, false) },
        { "VA-11", (-35.2, false) },

        // Washington - 8D, 2R
        { "WA-01", (-18.5, false) },
        { "WA-02", (-22.1, false) },
        { "WA-03", (12.5, true) },
        { "WA-04", (28.5, true) },
        { "WA-05", (8.5, true) },
        { "WA-06", (-15.8, false) },
        { "WA-07", (-58.2, false) },
        { "WA-08", (-5.2, false) },
        { "WA-09", (-38.5, false) },
        { "WA-10", (-25.8, false) },

        // West Virginia - 2R
        { "WV-01", (45.2, true) },
        { "WV-02", (48.5, true) },

        // Wisconsin - 4D, 4R
        { "WI-01", (12.5, true) },
        { "WI-02", (-32.5, false) },
        { "WI-03", (-5.2, false) },
        { "WI-04", (-48.2, false) },
        { "WI-05", (28.5, true) },
        { "WI-06", (22.1, true) },
        { "WI-07", (18.5, true) },
        { "WI-08", (20.2, true) },

        // Wyoming - At-large R
        { "WY-01", (55.8, true) }
    };

    /// <summary>
    /// 2025 Cook Partisan Voting Index (PVI) by congressional district, on the 2026 election
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
    /// Get the Cook PVI for a district. Positive = R lean, Negative = D lean.
    /// </summary>
    public static double GetDistrictPVI(string stateId, int districtNumber)
    {
        var key = $"{stateId}-{districtNumber:D2}";
        return DistrictPVI.TryGetValue(key, out var pvi) ? pvi : 0;
    }
}
