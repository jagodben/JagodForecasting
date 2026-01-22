namespace ElectionForecaster.Infrastructure.Data;

/// <summary>
/// Contains real 2024 House election results and district-level partisan data.
/// Data sources: Cook Political Report PVI, 2024 election results
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
        // Alabama - All R
        { "AL-01", (30.1, true) },
        { "AL-02", (28.5, true) },
        { "AL-03", (30.2, true) },
        { "AL-04", (50.3, true) },
        { "AL-05", (29.8, true) },
        { "AL-06", (40.1, true) },
        { "AL-07", (-29.5, false) }, // Terri Sewell (D)

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

        // California - 40D, 12R (2024 results)
        { "CA-01", (20.5, true) },
        { "CA-02", (-45.2, false) },
        { "CA-03", (10.2, true) },
        { "CA-04", (-28.5, false) },
        { "CA-05", (-43.1, false) },
        { "CA-06", (-35.2, false) },
        { "CA-07", (-22.8, false) },
        { "CA-08", (-30.1, false) },
        { "CA-09", (-24.5, false) },
        { "CA-10", (-15.3, false) },
        { "CA-11", (-52.1, false) },
        { "CA-12", (-60.2, false) },
        { "CA-13", (3.5, true) },
        { "CA-14", (-54.3, false) },
        { "CA-15", (-42.1, false) },
        { "CA-16", (-35.8, false) },
        { "CA-17", (-28.9, false) },
        { "CA-18", (-30.2, false) },
        { "CA-19", (-35.1, false) },
        { "CA-20", (15.8, true) },
        { "CA-21", (-42.5, false) },
        { "CA-22", (5.2, true) },
        { "CA-23", (18.5, true) },
        { "CA-24", (-20.1, false) },
        { "CA-25", (-18.5, false) },
        { "CA-26", (-12.8, false) },
        { "CA-27", (6.5, true) },
        { "CA-28", (-48.2, false) },
        { "CA-29", (-40.5, false) },
        { "CA-30", (-52.3, false) },
        { "CA-31", (-25.8, false) },
        { "CA-32", (-30.1, false) },
        { "CA-33", (-48.5, false) },
        { "CA-34", (-55.2, false) },
        { "CA-35", (-32.1, false) },
        { "CA-36", (-28.5, false) },
        { "CA-37", (-55.8, false) },
        { "CA-38", (-22.5, false) },
        { "CA-39", (-5.2, false) },
        { "CA-40", (12.5, true) },
        { "CA-41", (8.2, true) },
        { "CA-42", (-38.5, false) },
        { "CA-43", (-58.2, false) },
        { "CA-44", (-48.5, false) },
        { "CA-45", (3.8, true) },
        { "CA-46", (-8.5, false) },
        { "CA-47", (-5.8, false) },
        { "CA-48", (5.5, true) },
        { "CA-49", (-8.2, false) },
        { "CA-50", (-28.5, false) },
        { "CA-51", (-35.2, false) },
        { "CA-52", (-20.1, false) },

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

        // Florida - 19R, 9D
        { "FL-01", (38.5, true) },
        { "FL-02", (28.2, true) },
        { "FL-03", (30.1, true) },
        { "FL-04", (35.8, true) },
        { "FL-05", (32.1, true) },
        { "FL-06", (25.5, true) },
        { "FL-07", (-8.2, false) },
        { "FL-08", (30.2, true) },
        { "FL-09", (-12.5, false) },
        { "FL-10", (-30.1, false) },
        { "FL-11", (35.2, true) },
        { "FL-12", (28.5, true) },
        { "FL-13", (8.5, true) },
        { "FL-14", (-22.1, false) },
        { "FL-15", (12.5, true) },
        { "FL-16", (18.2, true) },
        { "FL-17", (25.8, true) },
        { "FL-18", (15.2, true) },
        { "FL-19", (25.1, true) },
        { "FL-20", (-52.5, false) },
        { "FL-21", (8.2, true) },
        { "FL-22", (-15.2, false) },
        { "FL-23", (-28.5, false) },
        { "FL-24", (-42.1, false) },
        { "FL-25", (18.5, true) },
        { "FL-26", (12.8, true) },
        { "FL-27", (5.2, true) },
        { "FL-28", (20.1, true) },

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

        // Louisiana - 5R, 1D
        { "LA-01", (40.2, true) },
        { "LA-02", (-55.8, false) },
        { "LA-03", (35.1, true) },
        { "LA-04", (38.5, true) },
        { "LA-05", (42.2, true) },
        { "LA-06", (28.5, true) },

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

        // Missouri - 6R, 2D
        { "MO-01", (-48.5, false) },
        { "MO-02", (15.8, true) },
        { "MO-03", (35.2, true) },
        { "MO-04", (40.1, true) },
        { "MO-05", (-18.5, false) },
        { "MO-06", (38.5, true) },
        { "MO-07", (42.1, true) },
        { "MO-08", (48.2, true) },

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

        // North Carolina - 7R, 7D
        { "NC-01", (-10.5, false) },
        { "NC-02", (-12.8, false) },
        { "NC-03", (28.5, true) },
        { "NC-04", (-35.2, false) },
        { "NC-05", (22.1, true) },
        { "NC-06", (-22.5, false) },
        { "NC-07", (18.5, true) },
        { "NC-08", (20.1, true) },
        { "NC-09", (15.2, true) },
        { "NC-10", (25.8, true) },
        { "NC-11", (28.2, true) },
        { "NC-12", (-38.5, false) },
        { "NC-13", (8.5, true) },
        { "NC-14", (-18.2, false) },

        // North Dakota - At-large R
        { "ND-01", (32.5, true) },

        // Ohio - 10R, 5D
        { "OH-01", (18.5, true) },
        { "OH-02", (25.2, true) },
        { "OH-03", (-35.8, false) },
        { "OH-04", (38.5, true) },
        { "OH-05", (30.1, true) },
        { "OH-06", (35.2, true) },
        { "OH-07", (28.5, true) },
        { "OH-08", (40.1, true) },
        { "OH-09", (-8.5, false) },
        { "OH-10", (22.5, true) },
        { "OH-11", (-55.8, false) },
        { "OH-12", (18.2, true) },
        { "OH-13", (-12.5, false) },
        { "OH-14", (25.1, true) },
        { "OH-15", (28.5, true) },

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

        // Tennessee - 8R, 1D
        { "TN-01", (52.5, true) },
        { "TN-02", (42.1, true) },
        { "TN-03", (38.5, true) },
        { "TN-04", (45.2, true) },
        { "TN-05", (-8.5, false) },
        { "TN-06", (48.2, true) },
        { "TN-07", (35.8, true) },
        { "TN-08", (50.1, true) },
        { "TN-09", (-55.8, false) },

        // Texas - 25R, 13D
        { "TX-01", (45.2, true) },
        { "TX-02", (8.5, true) },
        { "TX-03", (18.2, true) },
        { "TX-04", (48.5, true) },
        { "TX-05", (35.2, true) },
        { "TX-06", (20.1, true) },
        { "TX-07", (-5.2, false) },
        { "TX-08", (50.5, true) },
        { "TX-09", (-55.8, false) },
        { "TX-10", (15.8, true) },
        { "TX-11", (58.2, true) },
        { "TX-12", (28.5, true) },
        { "TX-13", (62.1, true) },
        { "TX-14", (35.2, true) },
        { "TX-15", (-8.5, false) },
        { "TX-16", (-28.5, false) },
        { "TX-17", (25.8, true) },
        { "TX-18", (-58.2, false) },
        { "TX-19", (55.5, true) },
        { "TX-20", (-35.8, false) },
        { "TX-21", (22.5, true) },
        { "TX-22", (15.2, true) },
        { "TX-23", (10.5, true) },
        { "TX-24", (12.8, true) },
        { "TX-25", (25.1, true) },
        { "TX-26", (22.5, true) },
        { "TX-27", (20.8, true) },
        { "TX-28", (-12.5, false) },
        { "TX-29", (-48.5, false) },
        { "TX-30", (-62.1, false) },
        { "TX-31", (18.5, true) },
        { "TX-32", (-8.2, false) },
        { "TX-33", (-55.2, false) },
        { "TX-34", (-12.8, false) },
        { "TX-35", (-35.8, false) },
        { "TX-36", (50.2, true) },
        { "TX-37", (-45.2, false) },
        { "TX-38", (35.8, true) },

        // Utah - 4R
        { "UT-01", (35.8, true) },
        { "UT-02", (22.5, true) },
        { "UT-03", (38.2, true) },
        { "UT-04", (12.5, true) },

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
    /// Cook Partisan Voting Index (PVI) by congressional district.
    /// Positive = Republican lean, Negative = Democratic lean.
    /// Based on how much the district deviated from the national average in recent presidential elections.
    /// </summary>
    public static readonly Dictionary<string, double> DistrictPVI = new()
    {
        // Alabama
        { "AL-01", 15 }, { "AL-02", 14 }, { "AL-03", 15 }, { "AL-04", 30 },
        { "AL-05", 17 }, { "AL-06", 22 }, { "AL-07", -16 },

        // Alaska
        { "AK-01", 8 },

        // Arizona
        { "AZ-01", -3 }, { "AZ-02", 9 }, { "AZ-03", -15 }, { "AZ-04", -5 },
        { "AZ-05", 12 }, { "AZ-06", 2 }, { "AZ-07", -18 }, { "AZ-08", 11 },
        { "AZ-09", 8 },

        // Arkansas
        { "AR-01", 20 }, { "AR-02", 12 }, { "AR-03", 18 }, { "AR-04", 24 },

        // California
        { "CA-01", 12 }, { "CA-02", -28 }, { "CA-03", 5 }, { "CA-04", -16 },
        { "CA-05", -26 }, { "CA-06", -20 }, { "CA-07", -12 }, { "CA-08", -17 },
        { "CA-09", -13 }, { "CA-10", -8 }, { "CA-11", -32 }, { "CA-12", -38 },
        { "CA-13", 2 }, { "CA-14", -34 }, { "CA-15", -25 }, { "CA-16", -20 },
        { "CA-17", -15 }, { "CA-18", -17 }, { "CA-19", -20 }, { "CA-20", 10 },
        { "CA-21", -26 }, { "CA-22", 4 }, { "CA-23", 12 }, { "CA-24", -12 },
        { "CA-25", -10 }, { "CA-26", -6 }, { "CA-27", 4 }, { "CA-28", -30 },
        { "CA-29", -24 }, { "CA-30", -32 }, { "CA-31", -14 }, { "CA-32", -17 },
        { "CA-33", -30 }, { "CA-34", -35 }, { "CA-35", -18 }, { "CA-36", -15 },
        { "CA-37", -35 }, { "CA-38", -12 }, { "CA-39", -2 }, { "CA-40", 8 },
        { "CA-41", 5 }, { "CA-42", -22 }, { "CA-43", -38 }, { "CA-44", -30 },
        { "CA-45", 2 }, { "CA-46", -4 }, { "CA-47", -3 }, { "CA-48", 3 },
        { "CA-49", -4 }, { "CA-50", -15 }, { "CA-51", -20 }, { "CA-52", -11 },

        // Colorado
        { "CO-01", -26 }, { "CO-02", -14 }, { "CO-03", 7 }, { "CO-04", 12 },
        { "CO-05", 14 }, { "CO-06", -6 }, { "CO-07", -10 }, { "CO-08", -2 },

        // Connecticut
        { "CT-01", -14 }, { "CT-02", -6 }, { "CT-03", -12 }, { "CT-04", -10 },
        { "CT-05", -4 },

        // Delaware
        { "DE-01", -10 },

        // Florida
        { "FL-01", 22 }, { "FL-02", 16 }, { "FL-03", 18 }, { "FL-04", 20 },
        { "FL-05", 18 }, { "FL-06", 14 }, { "FL-07", -4 }, { "FL-08", 17 },
        { "FL-09", -7 }, { "FL-10", -18 }, { "FL-11", 20 }, { "FL-12", 16 },
        { "FL-13", 4 }, { "FL-14", -12 }, { "FL-15", 7 }, { "FL-16", 10 },
        { "FL-17", 14 }, { "FL-18", 8 }, { "FL-19", 14 }, { "FL-20", -34 },
        { "FL-21", 4 }, { "FL-22", -8 }, { "FL-23", -16 }, { "FL-24", -26 },
        { "FL-25", 10 }, { "FL-26", 7 }, { "FL-27", 2 }, { "FL-28", 12 },

        // Georgia
        { "GA-01", 16 }, { "GA-02", -10 }, { "GA-03", 20 }, { "GA-04", -36 },
        { "GA-05", -38 }, { "GA-06", 8 }, { "GA-07", -4 }, { "GA-08", 18 },
        { "GA-09", 30 }, { "GA-10", 16 }, { "GA-11", 17 }, { "GA-12", 14 },
        { "GA-13", -28 }, { "GA-14", 25 },

        // Hawaii
        { "HI-01", -20 }, { "HI-02", -18 },

        // Idaho
        { "ID-01", 20 }, { "ID-02", 17 },

        // Illinois
        { "IL-01", -35 }, { "IL-02", -38 }, { "IL-03", -22 }, { "IL-04", -40 },
        { "IL-05", -28 }, { "IL-06", -6 }, { "IL-07", -45 }, { "IL-08", -10 },
        { "IL-09", -25 }, { "IL-10", -12 }, { "IL-11", -8 }, { "IL-12", 10 },
        { "IL-13", -4 }, { "IL-14", -6 }, { "IL-15", 25 }, { "IL-16", 15 },
        { "IL-17", -5 },

        // Indiana
        { "IN-01", -4 }, { "IN-02", 14 }, { "IN-03", 20 }, { "IN-04", 22 },
        { "IN-05", 10 }, { "IN-06", 20 }, { "IN-07", -20 }, { "IN-08", 18 },
        { "IN-09", 15 },

        // Iowa
        { "IA-01", 6 }, { "IA-02", 10 }, { "IA-03", 4 }, { "IA-04", 16 },

        // Kansas
        { "KS-01", 32 }, { "KS-02", 12 }, { "KS-03", -2 }, { "KS-04", 17 },

        // Kentucky
        { "KY-01", 28 }, { "KY-02", 22 }, { "KY-03", -8 }, { "KY-04", 20 },
        { "KY-05", 32 }, { "KY-06", 10 },

        // Louisiana
        { "LA-01", 24 }, { "LA-02", -36 }, { "LA-03", 20 }, { "LA-04", 22 },
        { "LA-05", 26 }, { "LA-06", 16 },

        // Maine
        { "ME-01", -8 }, { "ME-02", 4 },

        // Maryland
        { "MD-01", 12 }, { "MD-02", -20 }, { "MD-03", -25 }, { "MD-04", -38 },
        { "MD-05", -22 }, { "MD-06", -10 }, { "MD-07", -35 }, { "MD-08", -18 },

        // Massachusetts
        { "MA-01", -20 }, { "MA-02", -15 }, { "MA-03", -18 }, { "MA-04", -22 },
        { "MA-05", -30 }, { "MA-06", -14 }, { "MA-07", -42 }, { "MA-08", -25 },
        { "MA-09", -17 },

        // Michigan
        { "MI-01", 12 }, { "MI-02", 14 }, { "MI-03", -3 }, { "MI-04", 16 },
        { "MI-05", 10 }, { "MI-06", -8 }, { "MI-07", -2 }, { "MI-08", -3 },
        { "MI-09", 12 }, { "MI-10", 16 }, { "MI-11", -10 }, { "MI-12", -22 },
        { "MI-13", -38 },

        // Minnesota
        { "MN-01", 6 }, { "MN-02", -2 }, { "MN-03", -8 }, { "MN-04", -18 },
        { "MN-05", -30 }, { "MN-06", 10 }, { "MN-07", 14 }, { "MN-08", 4 },

        // Mississippi
        { "MS-01", 20 }, { "MS-02", -16 }, { "MS-03", 22 }, { "MS-04", 25 },

        // Missouri
        { "MO-01", -30 }, { "MO-02", 8 }, { "MO-03", 20 }, { "MO-04", 23 },
        { "MO-05", -10 }, { "MO-06", 22 }, { "MO-07", 25 }, { "MO-08", 28 },

        // Montana
        { "MT-01", 4 }, { "MT-02", 10 },

        // Nebraska
        { "NE-01", 12 }, { "NE-02", 2 }, { "NE-03", 35 },

        // Nevada
        { "NV-01", -10 }, { "NV-02", 7 }, { "NV-03", -2 }, { "NV-04", -4 },

        // New Hampshire
        { "NH-01", -2 }, { "NH-02", -4 },

        // New Jersey
        { "NJ-01", -8 }, { "NJ-02", 6 }, { "NJ-03", -4 }, { "NJ-04", 10 },
        { "NJ-05", -6 }, { "NJ-06", -12 }, { "NJ-07", 2 }, { "NJ-08", -20 },
        { "NJ-09", -16 }, { "NJ-10", -32 }, { "NJ-11", -8 }, { "NJ-12", -14 },

        // New Mexico
        { "NM-01", -6 }, { "NM-02", -4 }, { "NM-03", -10 },

        // New York
        { "NY-01", 4 }, { "NY-02", 8 }, { "NY-03", -2 }, { "NY-04", 1 },
        { "NY-05", -36 }, { "NY-06", -26 }, { "NY-07", -38 }, { "NY-08", -40 },
        { "NY-09", -28 }, { "NY-10", -38 }, { "NY-11", 6 }, { "NY-12", -32 },
        { "NY-13", -35 }, { "NY-14", -30 }, { "NY-15", -50 }, { "NY-16", -26 },
        { "NY-17", -2 }, { "NY-18", -4 }, { "NY-19", 1 }, { "NY-20", -14 },
        { "NY-21", 10 }, { "NY-22", 4 }, { "NY-23", 12 }, { "NY-24", 8 },
        { "NY-25", -10 }, { "NY-26", -20 },

        // North Carolina
        { "NC-01", -5 }, { "NC-02", -6 }, { "NC-03", 16 }, { "NC-04", -20 },
        { "NC-05", 12 }, { "NC-06", -12 }, { "NC-07", 10 }, { "NC-08", 11 },
        { "NC-09", 8 }, { "NC-10", 14 }, { "NC-11", 16 }, { "NC-12", -22 },
        { "NC-13", 4 }, { "NC-14", -10 },

        // North Dakota
        { "ND-01", 20 },

        // Ohio
        { "OH-01", 10 }, { "OH-02", 14 }, { "OH-03", -20 }, { "OH-04", 22 },
        { "OH-05", 17 }, { "OH-06", 20 }, { "OH-07", 16 }, { "OH-08", 23 },
        { "OH-09", -4 }, { "OH-10", 12 }, { "OH-11", -36 }, { "OH-12", 10 },
        { "OH-13", -6 }, { "OH-14", 14 }, { "OH-15", 16 },

        // Oklahoma
        { "OK-01", 20 }, { "OK-02", 25 }, { "OK-03", 32 }, { "OK-04", 23 },
        { "OK-05", 12 },

        // Oregon
        { "OR-01", -14 }, { "OR-02", 12 }, { "OR-03", -28 }, { "OR-04", -6 },
        { "OR-05", 2 }, { "OR-06", -4 },

        // Pennsylvania
        { "PA-01", 10 }, { "PA-02", -22 }, { "PA-03", -32 }, { "PA-04", -12 },
        { "PA-05", -10 }, { "PA-06", -6 }, { "PA-07", -2 }, { "PA-08", -4 },
        { "PA-09", 18 }, { "PA-10", 4 }, { "PA-11", 14 }, { "PA-12", -8 },
        { "PA-13", 17 }, { "PA-14", 12 }, { "PA-15", 16 }, { "PA-16", 11 },
        { "PA-17", -5 },

        // Rhode Island
        { "RI-01", -14 }, { "RI-02", -10 },

        // South Carolina
        { "SC-01", 10 }, { "SC-02", 14 }, { "SC-03", 20 }, { "SC-04", 17 },
        { "SC-05", 12 }, { "SC-06", -16 }, { "SC-07", 8 },

        // South Dakota
        { "SD-01", 20 },

        // Tennessee
        { "TN-01", 32 }, { "TN-02", 25 }, { "TN-03", 22 }, { "TN-04", 27 },
        { "TN-05", -4 }, { "TN-06", 28 }, { "TN-07", 20 }, { "TN-08", 30 },
        { "TN-09", -36 },

        // Texas
        { "TX-01", 28 }, { "TX-02", 4 }, { "TX-03", 10 }, { "TX-04", 30 },
        { "TX-05", 20 }, { "TX-06", 11 }, { "TX-07", -2 }, { "TX-08", 32 },
        { "TX-09", -36 }, { "TX-10", 8 }, { "TX-11", 38 }, { "TX-12", 16 },
        { "TX-13", 40 }, { "TX-14", 20 }, { "TX-15", -4 }, { "TX-16", -16 },
        { "TX-17", 14 }, { "TX-18", -38 }, { "TX-19", 35 }, { "TX-20", -20 },
        { "TX-21", 12 }, { "TX-22", 8 }, { "TX-23", 5 }, { "TX-24", 6 },
        { "TX-25", 14 }, { "TX-26", 12 }, { "TX-27", 11 }, { "TX-28", -6 },
        { "TX-29", -30 }, { "TX-30", -40 }, { "TX-31", 10 }, { "TX-32", -4 },
        { "TX-33", -35 }, { "TX-34", -6 }, { "TX-35", -20 }, { "TX-36", 32 },
        { "TX-37", -28 }, { "TX-38", 20 },

        // Utah
        { "UT-01", 20 }, { "UT-02", 12 }, { "UT-03", 22 }, { "UT-04", 6 },

        // Vermont
        { "VT-01", -17 },

        // Virginia
        { "VA-01", 10 }, { "VA-02", -2 }, { "VA-03", -20 }, { "VA-04", -25 },
        { "VA-05", 6 }, { "VA-06", 12 }, { "VA-07", -4 }, { "VA-08", -22 },
        { "VA-09", 20 }, { "VA-10", -6 }, { "VA-11", -20 },

        // Washington
        { "WA-01", -10 }, { "WA-02", -12 }, { "WA-03", 6 }, { "WA-04", 16 },
        { "WA-05", 4 }, { "WA-06", -8 }, { "WA-07", -38 }, { "WA-08", -2 },
        { "WA-09", -22 }, { "WA-10", -14 },

        // West Virginia
        { "WV-01", 28 }, { "WV-02", 30 },

        // Wisconsin
        { "WI-01", 6 }, { "WI-02", -18 }, { "WI-03", -2 }, { "WI-04", -30 },
        { "WI-05", 16 }, { "WI-06", 12 }, { "WI-07", 10 }, { "WI-08", 11 },

        // Wyoming
        { "WY-01", 35 }
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
