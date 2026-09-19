namespace ElectionForecaster.Infrastructure.Data;

/// <summary>
/// Viable independent candidates who are the real challenger in a race — more competitive than the
/// same-side major-party nominee — and so occupy the challenger slot in the two-way forecast instead
/// of a token major-party candidate. Hand-picked (static): most 2026 races have placeholder nominees
/// and no independent polling, so an editorial designation is more reliable than a poll-driven rule.
///
/// The independent takes the challenger ("D") slot, so the forecast's Dem-side probability/margin is
/// really theirs; the UI shows them with their true (Independent) party and a distinct color. Their
/// <see cref="Challenger.PriorMargin"/> and (in <c>PolymarketClient</c>) the market they're mapped to
/// must reflect the independent, not a generic Democrat, or the blend understates them badly.
/// </summary>
public static class IndependentChallengers
{
    /// <param name="Name">Display name of the independent.</param>
    /// <param name="ReplacesDem">True if the independent displaces the Democrat as the challenger to a
    /// Republican (the only configuration currently modeled).</param>
    /// <param name="PriorMargin">The seat's realistic prior as a challenger margin (points, D+ positive)
    /// reflecting THIS independent's demonstrated support, not a generic Democrat.</param>
    public sealed record Challenger(string Name, bool ReplacesDem, double PriorMargin);

    private static readonly Dictionary<string, Challenger> ByRace = new()
    {
        // Dan Osborn (I) is the viable challenger to appointed R incumbent Pete Ricketts — the token
        // Democrat is a non-factor. Osborn took ~46.5% statewide vs Fischer in 2024, so a challenger
        // margin near R+6 is a far better prior than the seat's generic R+25 (Ricketts' 2024 special).
        ["NE-SEN-2026"] = new("Dan Osborn", ReplacesDem: true, PriorMargin: -6),

        // Todd Achilles (I), a former Democratic state representative, is the challenger Idaho's
        // article and its poll aggregate are built around — the Democrat, David Roth, was being
        // pressed by his own county party to prove viability. Same shape in South Dakota, where
        // Brian Bengs (the 2022 Democratic nominee) now runs as an independent.
        //
        // Neither has Osborn's demonstrated statewide independent result, so their priors stay far
        // more conservative than his -6. Each takes the seat's generic-Democrat prior and closes
        // half the gap the same pollster measured between the Democrat and the independent:
        //   Idaho   Bullfinch: Risch +17 over Roth, then Achilles +5 over Risch -> 22pt gap;
        //           -29 + 11 = -18.
        //   S. Dak. PPP:       Rounds +25 over Beaudion, then +3 over Bengs   -> 22pt gap;
        //           -31 + 11 = -20.
        // Half the gap, not all of it: those polls are already the polling input, and this term
        // must not double-count them.
        ["ID-SEN-2026"] = new("Todd Achilles", ReplacesDem: true, PriorMargin: -18),
        ["SD-SEN-2026"] = new("Brian Bengs", ReplacesDem: true, PriorMargin: -20),
    };

    public static Challenger? Get(string raceId) => ByRace.GetValueOrDefault(raceId);

    public static bool Has(string raceId) => ByRace.ContainsKey(raceId);

    /// <summary>The independent's realistic prior margin for this race, or null if none is designated.</summary>
    public static double? GetPriorMargin(string raceId) =>
        ByRace.TryGetValue(raceId, out var c) ? c.PriorMargin : null;
}
