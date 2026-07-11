using ElectionForecaster.Core.Enums;

namespace ElectionForecaster.Backtest;

/// <summary>
/// The prior same-seat result for each race in <see cref="HistoricalData.Races"/> — what the
/// live model's StatewidePriorResults table would have contained at the time. Senate seats use
/// the previous election of the same class (2012 for the 2018 cycle, 2016 for 2022; specials use
/// the seat's most recent contest). Governors use the previous gubernatorial election (2014 for
/// 2018, 2018 for 2022; NH's two-year term uses 2016). Margins are Dem − Rep in points, curated
/// to the same "accurate to a point or two" standard as HistoricalData, capped at ±45.
/// PriorYear tags the cycle so the environment-adjusted variant can subtract that year's mood.
/// </summary>
public static class HistoricalPriors
{
    public readonly record struct Prior(double DemMargin, int PriorYear);

    /// <summary>National environment (Dem House-vote margin, points) for prior cycles.</summary>
    public static readonly Dictionary<int, double> CycleEnvironment = new()
    {
        [2012] = 1.2,
        [2014] = -5.7,
        [2016] = -1.1,
        [2018] = 8.6,
        [2020] = 3.1,
        [2021] = 3.1,   // GA runoffs, treated as the 2020 environment
    };

    public static readonly Dictionary<(int Year, string State, RaceType Office), Prior> Priors = new()
    {
        // ---- 2018 Senate (prior = 2012 same seat unless noted) ----
        [(2018, "AZ", RaceType.Senate)] = new(-3.0, 2012),   // Flake d. Carmona
        [(2018, "NV", RaceType.Senate)] = new(-1.2, 2012),   // Heller d. Berkley
        [(2018, "MT", RaceType.Senate)] = new(3.7, 2012),    // Tester
        [(2018, "WV", RaceType.Senate)] = new(24.1, 2012),   // Manchin
        [(2018, "IN", RaceType.Senate)] = new(5.7, 2012),    // Donnelly d. Mourdock
        [(2018, "MO", RaceType.Senate)] = new(15.7, 2012),   // McCaskill d. Akin
        [(2018, "ND", RaceType.Senate)] = new(0.9, 2012),    // Heitkamp
        [(2018, "FL", RaceType.Senate)] = new(13.0, 2012),   // Nelson d. Mack
        [(2018, "TX", RaceType.Senate)] = new(-15.8, 2012),  // Cruz
        [(2018, "TN", RaceType.Senate)] = new(-34.5, 2012),  // Corker (open in 2018)
        [(2018, "WI", RaceType.Senate)] = new(5.6, 2012),    // Baldwin
        [(2018, "MI", RaceType.Senate)] = new(20.8, 2012),   // Stabenow
        [(2018, "OH", RaceType.Senate)] = new(6.0, 2012),    // Brown
        [(2018, "PA", RaceType.Senate)] = new(9.1, 2012),    // Casey
        [(2018, "MN", RaceType.Senate)] = new(34.7, 2012),   // Klobuchar
        [(2018, "NJ", RaceType.Senate)] = new(19.5, 2012),   // Menendez
        [(2018, "MA", RaceType.Senate)] = new(7.5, 2012),    // Warren d. Brown
        [(2018, "NM", RaceType.Senate)] = new(5.7, 2012),    // Heinrich
        [(2018, "VA", RaceType.Senate)] = new(5.9, 2012),    // Kaine
        [(2018, "CT", RaceType.Senate)] = new(11.9, 2012),   // Murphy d. McMahon
        [(2018, "MS", RaceType.Senate)] = new(-22.0, 2014),  // special: seat last held by Cochran (2014)
        [(2018, "NE", RaceType.Senate)] = new(-15.6, 2012),  // Fischer d. Kerrey
        [(2018, "WY", RaceType.Senate)] = new(-45.0, 2012),  // Barrasso (capped)
        [(2018, "WA", RaceType.Senate)] = new(20.8, 2012),   // Cantwell

        // ---- 2018 Governor (prior = 2014; NH two-year term = 2016) ----
        [(2018, "FL", RaceType.Governor)] = new(-1.1, 2014),  // Scott d. Crist
        [(2018, "GA", RaceType.Governor)] = new(-7.8, 2014),  // Deal d. Carter
        [(2018, "OH", RaceType.Governor)] = new(-30.7, 2014), // Kasich landslide
        [(2018, "WI", RaceType.Governor)] = new(-5.7, 2014),  // Walker d. Burke
        [(2018, "KS", RaceType.Governor)] = new(-3.7, 2014),  // Brownback d. Davis
        [(2018, "MI", RaceType.Governor)] = new(-4.0, 2014),  // Snyder d. Schauer
        [(2018, "IL", RaceType.Governor)] = new(-3.9, 2014),  // Rauner d. Quinn
        [(2018, "NV", RaceType.Governor)] = new(-45.0, 2014), // Sandoval landslide (capped)
        [(2018, "ME", RaceType.Governor)] = new(-4.9, 2014),  // LePage d. Michaud
        [(2018, "NM", RaceType.Governor)] = new(-14.6, 2014), // Martinez d. King
        [(2018, "CO", RaceType.Governor)] = new(3.3, 2014),   // Hickenlooper d. Beauprez
        [(2018, "IA", RaceType.Governor)] = new(-21.7, 2014), // Branstad d. Hatch
        [(2018, "AZ", RaceType.Governor)] = new(-12.0, 2014), // Ducey d. DuVal
        [(2018, "NH", RaceType.Governor)] = new(-2.3, 2016),  // Sununu d. Van Ostern
        [(2018, "TX", RaceType.Governor)] = new(-20.4, 2014), // Abbott d. Davis
        [(2018, "MD", RaceType.Governor)] = new(-3.8, 2014),  // Hogan d. Brown
        [(2018, "PA", RaceType.Governor)] = new(9.9, 2014),   // Wolf d. Corbett
        [(2018, "NY", RaceType.Governor)] = new(13.9, 2014),  // Cuomo d. Astorino

        // ---- 2022 Senate (prior = 2016 same seat; specials noted) ----
        [(2022, "GA", RaceType.Senate)] = new(2.1, 2021),    // Warnock runoff (seat's last contest)
        [(2022, "AZ", RaceType.Senate)] = new(2.4, 2020),    // Kelly special (McCain seat)
        [(2022, "NV", RaceType.Senate)] = new(2.4, 2016),    // Cortez Masto
        [(2022, "PA", RaceType.Senate)] = new(-1.4, 2016),   // Toomey (open in 2022)
        [(2022, "NH", RaceType.Senate)] = new(0.1, 2016),    // Hassan d. Ayotte
        [(2022, "WI", RaceType.Senate)] = new(-3.4, 2016),   // Johnson d. Feingold
        [(2022, "OH", RaceType.Senate)] = new(-21.1, 2016),  // Portman (open in 2022)
        [(2022, "NC", RaceType.Senate)] = new(-5.7, 2016),   // Burr (open in 2022)
        [(2022, "FL", RaceType.Senate)] = new(-7.7, 2016),   // Rubio
        [(2022, "CO", RaceType.Senate)] = new(5.7, 2016),    // Bennet
        [(2022, "WA", RaceType.Senate)] = new(18.2, 2016),   // Murray
        [(2022, "CT", RaceType.Senate)] = new(28.3, 2016),   // Blumenthal
        [(2022, "IL", RaceType.Senate)] = new(15.1, 2016),   // Duckworth d. Kirk
        [(2022, "NY", RaceType.Senate)] = new(43.0, 2016),   // Schumer
        [(2022, "MO", RaceType.Senate)] = new(-2.8, 2016),   // Blunt d. Kander (open in 2022)
        [(2022, "IN", RaceType.Senate)] = new(-9.7, 2016),   // Young d. Bayh
        [(2022, "KY", RaceType.Senate)] = new(-14.6, 2016),  // Paul
        [(2022, "IA", RaceType.Senate)] = new(-24.4, 2016),  // Grassley
        [(2022, "SC", RaceType.Senate)] = new(-23.6, 2016),  // Scott

        // ---- 2022 Governor (prior = 2018; margins match HistoricalData's 2018 rows) ----
        [(2022, "AZ", RaceType.Governor)] = new(-14.2, 2018),
        [(2022, "WI", RaceType.Governor)] = new(1.1, 2018),
        [(2022, "MI", RaceType.Governor)] = new(9.5, 2018),
        [(2022, "PA", RaceType.Governor)] = new(17.1, 2018),
        [(2022, "NV", RaceType.Governor)] = new(4.1, 2018),
        [(2022, "KS", RaceType.Governor)] = new(5.0, 2018),
        [(2022, "GA", RaceType.Governor)] = new(-1.4, 2018),
        [(2022, "FL", RaceType.Governor)] = new(-0.4, 2018),
        [(2022, "OH", RaceType.Governor)] = new(-3.7, 2018),
        [(2022, "TX", RaceType.Governor)] = new(-13.3, 2018),
        [(2022, "NY", RaceType.Governor)] = new(23.4, 2018),
        [(2022, "ME", RaceType.Governor)] = new(7.9, 2018),
        [(2022, "MN", RaceType.Governor)] = new(11.5, 2018),  // Walz d. Johnson
        [(2022, "NM", RaceType.Governor)] = new(14.5, 2018),
        [(2022, "CO", RaceType.Governor)] = new(10.6, 2018),
        [(2022, "OK", RaceType.Governor)] = new(-12.1, 2018), // Stitt d. Edmondson
        [(2022, "IA", RaceType.Governor)] = new(-2.9, 2018),
        [(2022, "MD", RaceType.Governor)] = new(-12.0, 2018), // Hogan (open in 2022 — crossover prior)
        [(2022, "IL", RaceType.Governor)] = new(15.7, 2018),
        [(2022, "OR", RaceType.Governor)] = new(6.4, 2018),   // Brown d. Buehler
        [(2022, "MA", RaceType.Governor)] = new(-33.5, 2018), // Baker landslide (open in 2022 — crossover prior)
    };

    public static Prior? Get(HistoricalRace race)
        => Priors.TryGetValue((race.Year, race.State, race.Office), out var p) ? p : null;
}
