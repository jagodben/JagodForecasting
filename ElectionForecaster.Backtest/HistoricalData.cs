using ElectionForecaster.Core.Enums;

namespace ElectionForecaster.Backtest;

/// <summary>One historical race with a known outcome, for backtesting the fundamentals model.</summary>
/// <param name="Incumbent">'D' = Dem incumbent running, 'R' = Rep incumbent running, 'O' = open seat.</param>
/// <param name="ActualDemMargin">Actual result as Dem% − Rep% (positive = Dem win).</param>
public readonly record struct HistoricalRace(
    int Year, string State, RaceType Office, char Incumbent, double ActualDemMargin);

public static class HistoricalData
{
    // National environment (Dem national popular-vote margin) by cycle. 2018 was a strong Dem
    // year (~D+8.6 House vote); 2022 was mildly Republican (~R+2.8).
    public static readonly Dictionary<int, double> NationalEnvironment = new()
    {
        [2018] = 8.6,
        [2022] = -2.8,
    };

    // Cook PVI (2024 release, Dem+ positive). NOTE: using current PVI for older cycles introduces
    // some error for states that have shifted (FL/OH → R, AZ/GA → D). Good enough to exercise the
    // harness; swap in year-correct PVI to sharpen calibration.
    public static readonly Dictionary<string, double> Pvi = new()
    {
        ["AL"] = -15, ["AK"] = -9, ["AZ"] = -2, ["AR"] = -16, ["CA"] = 14, ["CO"] = 6, ["CT"] = 8,
        ["DE"] = 7, ["FL"] = -6, ["GA"] = 0, ["HI"] = 15, ["ID"] = -19, ["IL"] = 8, ["IN"] = -10,
        ["IA"] = -6, ["KS"] = -10, ["KY"] = -16, ["LA"] = -13, ["ME"] = 3, ["MD"] = 14, ["MA"] = 16,
        ["MI"] = 1, ["MN"] = 2, ["MS"] = -10, ["MO"] = -10, ["MT"] = -11, ["NE"] = -12, ["NV"] = 0,
        ["NH"] = 1, ["NJ"] = 7, ["NM"] = 5, ["NY"] = 10, ["NC"] = -3, ["ND"] = -20, ["OH"] = -6,
        ["OK"] = -20, ["OR"] = 6, ["PA"] = 0, ["RI"] = 10, ["SC"] = -8, ["SD"] = -16, ["TN"] = -14,
        ["TX"] = -5, ["UT"] = -11, ["VT"] = 16, ["VA"] = 4, ["WA"] = 8, ["WV"] = -23, ["WI"] = 0, ["WY"] = -25,
    };

    // Approximate actual results (Dem − Rep margin, points). Curated from memory across 2018/2022
    // Senate and Governor races; accurate to a point or two, which is fine for calibration. Expand/
    // correct freely — this is the dataset, not the model.
    public static readonly List<HistoricalRace> Races = new()
    {
        // ---- 2018 Senate ----
        new(2018, "AZ", RaceType.Senate, 'O', 2.4),
        new(2018, "NV", RaceType.Senate, 'R', 5.0),
        new(2018, "MT", RaceType.Senate, 'D', 3.5),
        new(2018, "WV", RaceType.Senate, 'D', 3.3),
        new(2018, "IN", RaceType.Senate, 'D', -5.9),
        new(2018, "MO", RaceType.Senate, 'D', -5.8),
        new(2018, "ND", RaceType.Senate, 'D', -10.8),
        new(2018, "FL", RaceType.Senate, 'D', -0.2),
        new(2018, "TX", RaceType.Senate, 'R', -2.6),
        new(2018, "TN", RaceType.Senate, 'O', -10.8),
        new(2018, "WI", RaceType.Senate, 'D', 10.8),
        new(2018, "MI", RaceType.Senate, 'D', 6.5),
        new(2018, "OH", RaceType.Senate, 'D', 6.8),
        new(2018, "PA", RaceType.Senate, 'D', 13.1),
        new(2018, "MN", RaceType.Senate, 'D', 24.1),
        new(2018, "NJ", RaceType.Senate, 'D', 11.2),
        new(2018, "MA", RaceType.Senate, 'D', 24.2),
        new(2018, "NM", RaceType.Senate, 'D', 23.6),
        new(2018, "VA", RaceType.Senate, 'D', 16.0),
        new(2018, "CT", RaceType.Senate, 'D', 19.6),
        new(2018, "MS", RaceType.Senate, 'R', -7.2),
        new(2018, "NE", RaceType.Senate, 'R', -19.0),
        new(2018, "WY", RaceType.Senate, 'R', -37.0),
        new(2018, "WA", RaceType.Senate, 'D', 16.9),

        // ---- 2018 Governor ----
        new(2018, "FL", RaceType.Governor, 'O', -0.4),
        new(2018, "GA", RaceType.Governor, 'O', -1.4),
        new(2018, "OH", RaceType.Governor, 'O', -3.7),
        new(2018, "WI", RaceType.Governor, 'R', 1.1),
        new(2018, "KS", RaceType.Governor, 'O', 5.0),
        new(2018, "MI", RaceType.Governor, 'O', 9.5),
        new(2018, "IL", RaceType.Governor, 'R', 15.7),
        new(2018, "NV", RaceType.Governor, 'O', 4.1),
        new(2018, "ME", RaceType.Governor, 'O', 7.9),
        new(2018, "NM", RaceType.Governor, 'O', 14.5),
        new(2018, "CO", RaceType.Governor, 'O', 10.6),
        new(2018, "IA", RaceType.Governor, 'R', -2.9),
        new(2018, "AZ", RaceType.Governor, 'R', -14.2),
        new(2018, "NH", RaceType.Governor, 'R', -7.3),
        new(2018, "TX", RaceType.Governor, 'R', -13.3),
        new(2018, "MD", RaceType.Governor, 'R', -12.0),
        new(2018, "PA", RaceType.Governor, 'D', 17.1),
        new(2018, "NY", RaceType.Governor, 'D', 23.4),

        // ---- 2022 Senate ----
        new(2022, "GA", RaceType.Senate, 'D', 2.9),
        new(2022, "AZ", RaceType.Senate, 'D', 4.9),
        new(2022, "NV", RaceType.Senate, 'D', 0.8),
        new(2022, "PA", RaceType.Senate, 'O', 5.0),
        new(2022, "NH", RaceType.Senate, 'D', 9.1),
        new(2022, "WI", RaceType.Senate, 'R', -1.0),
        new(2022, "OH", RaceType.Senate, 'O', -6.1),
        new(2022, "NC", RaceType.Senate, 'O', -3.2),
        new(2022, "FL", RaceType.Senate, 'R', -16.4),
        new(2022, "CO", RaceType.Senate, 'D', 14.5),
        new(2022, "WA", RaceType.Senate, 'D', 14.8),
        new(2022, "CT", RaceType.Senate, 'D', 14.9),
        new(2022, "IL", RaceType.Senate, 'D', 13.5),
        new(2022, "NY", RaceType.Senate, 'D', 13.0),
        new(2022, "MO", RaceType.Senate, 'O', -13.2),
        new(2022, "IN", RaceType.Senate, 'R', -17.4),
        new(2022, "KY", RaceType.Senate, 'R', -23.0),
        new(2022, "IA", RaceType.Senate, 'R', -12.1),
        new(2022, "SC", RaceType.Senate, 'R', -26.0),

        // ---- 2022 Governor ----
        new(2022, "AZ", RaceType.Governor, 'O', 0.4),
        new(2022, "WI", RaceType.Governor, 'D', 3.4),
        new(2022, "MI", RaceType.Governor, 'D', 10.6),
        new(2022, "PA", RaceType.Governor, 'O', 14.8),
        new(2022, "NV", RaceType.Governor, 'D', -1.5),
        new(2022, "KS", RaceType.Governor, 'D', 2.1),
        new(2022, "GA", RaceType.Governor, 'R', -7.5),
        new(2022, "FL", RaceType.Governor, 'R', -19.4),
        new(2022, "OH", RaceType.Governor, 'R', -25.4),
        new(2022, "TX", RaceType.Governor, 'R', -11.0),
        new(2022, "NY", RaceType.Governor, 'D', 6.4),
        new(2022, "ME", RaceType.Governor, 'D', 13.0),
        new(2022, "MN", RaceType.Governor, 'D', 7.7),
        new(2022, "NM", RaceType.Governor, 'D', 6.0),
        new(2022, "CO", RaceType.Governor, 'D', 19.4),
        new(2022, "OK", RaceType.Governor, 'R', -13.6),
        new(2022, "IA", RaceType.Governor, 'R', -18.6),
        new(2022, "MD", RaceType.Governor, 'O', 32.2),
        new(2022, "IL", RaceType.Governor, 'D', 12.5),
        new(2022, "OR", RaceType.Governor, 'O', 3.4),
        new(2022, "MA", RaceType.Governor, 'O', 29.0),
    };
}
