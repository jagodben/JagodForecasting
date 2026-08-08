using ElectionForecaster.Infrastructure.DataSources.Models;
using ElectionForecaster.Infrastructure.DataSources.Polling;

namespace ElectionForecaster.Tests;

/// <summary>
/// Pins how undecided-primary polls enter the model: each stored row is one matchup exactly as
/// published, and MatchupBlender.Collapse folds a poll's matchup rows into one effective poll —
/// the cross-matchup mean while primaries are open, the settled nominees' matchup once decided —
/// with the margin spread feeding the nominee-uncertainty SE term.
/// </summary>
public class MatchupBlendTests
{
    private static readonly DateTime PollDate = new(2026, 7, 16);

    private static PollData Poll(double dem, double rep, string pollster = "Marquette", DateTime? date = null,
        string? demCandidate = null, string? repCandidate = null)
        => new()
        {
            RaceId = "WI-GOV-2026",
            Pollster = pollster,
            Date = date ?? PollDate,
            DemPercent = dem,
            RepPercent = rep,
            DemCandidate = demCandidate,
            RepCandidate = repCandidate,
            SampleSize = 695,
            Population = "LV",
        };

    [Fact]
    public void Collapse_SingleMatchup_PassesThroughUnchanged()
    {
        var poll = Poll(47, 42);
        var result = MatchupBlender.Collapse(new[] { poll });

        var effective = Assert.Single(result);
        Assert.Same(poll, effective);
        Assert.Equal(1, effective.MatchupCount);
        Assert.Equal(0, effective.MatchupSpread, 6);
    }

    [Fact]
    public void Collapse_MultipleMatchups_AveragesWithSpread()
    {
        // The WI-GOV shape: Barnes–Tiffany D+5 and Hong–Tiffany R+3 from the same poll.
        var barnes = Poll(47, 42, demCandidate: "Mandela Barnes", repCandidate: "Tom Tiffany");
        var hong = Poll(40, 43, demCandidate: "Francesca Hong", repCandidate: "Tom Tiffany");
        var result = MatchupBlender.Collapse(new[] { barnes, hong });

        var effective = Assert.Single(result);
        Assert.Equal(43.5, effective.DemPercent, 6);
        Assert.Equal(42.5, effective.RepPercent, 6);
        Assert.Equal(2, effective.MatchupCount);
        // Spread: D+5 down to R+3 = 8 points of nominee dependence.
        Assert.Equal(8, effective.MatchupSpread, 6);
        // The published rows must stay untouched — display shows them as they came.
        Assert.Equal(47, barnes.DemPercent);
        Assert.Equal(1, barnes.MatchupCount);
    }

    [Fact]
    public void Collapse_SettledNominees_SelectTheirMatchup()
    {
        // Once Hong wins the primary, the Barnes table is a stale hypothetical: the effective
        // poll must be the Hong–Tiffany row itself — no blend, no spread.
        var result = MatchupBlender.Collapse(new[]
        {
            Poll(47, 42, demCandidate: "Mandela Barnes", repCandidate: "Tom Tiffany"),
            Poll(40, 43, demCandidate: "Francesca Hong", repCandidate: "Tom Tiffany"),
        }, demNominee: "Francesca Hong", repNominee: "Tom Tiffany");

        var effective = Assert.Single(result);
        Assert.Equal(40, effective.DemPercent);
        Assert.Equal(43, effective.RepPercent);
        Assert.Equal(1, effective.MatchupCount);
        Assert.Equal(0, effective.MatchupSpread, 6);
    }

    [Fact]
    public void Collapse_OneSideSettled_BlendsOnlyThatSidesMatchups()
    {
        // Rep primary decided (Tiffany), Dem still open: the Michels table drops out; the
        // remaining Tiffany matchups still blend with their spread.
        var result = MatchupBlender.Collapse(new[]
        {
            Poll(47, 42, demCandidate: "Mandela Barnes", repCandidate: "Tom Tiffany"),
            Poll(40, 43, demCandidate: "Francesca Hong", repCandidate: "Tom Tiffany"),
            Poll(49, 40, demCandidate: "Mandela Barnes", repCandidate: "Tim Michels"),
        }, repNominee: "Tom Tiffany");

        var effective = Assert.Single(result);
        Assert.Equal(2, effective.MatchupCount);
        Assert.Equal(43.5, effective.DemPercent, 6);
        Assert.Equal(8, effective.MatchupSpread, 6);
    }

    [Fact]
    public void Collapse_PollOfOnlyDefeatedCandidates_IsDropped()
    {
        // The Michigan scenario: the settled nominee appears in none of this poll's matchups
        // (every pairing stars a candidate who lost their primary), so the poll says nothing
        // about the race as it now stands and must not enter the model at all. A same-day poll
        // from another pollster that did test the nominee survives.
        var result = MatchupBlender.Collapse(new[]
        {
            Poll(47, 42, demCandidate: "Mandela Barnes", repCandidate: "Tom Tiffany"),
            Poll(40, 43, demCandidate: "Francesca Hong", repCandidate: "Tom Tiffany"),
            Poll(45, 44, "Wedgewood", demCandidate: "Kelda Roys", repCandidate: "Tom Tiffany"),
        }, demNominee: "Kelda Roys");

        var effective = Assert.Single(result);
        Assert.Equal("Wedgewood", effective.Pollster);
        Assert.Equal(45, effective.DemPercent);
    }

    [Fact]
    public void Collapse_LegacyRowsWithoutCandidates_SurviveSettledNominees()
    {
        // Rows stored before matchups were tracked have no candidate names — they were the
        // published nominee matchup of their day, so a settled primary must not drop them.
        var legacy = Poll(46, 45);
        var result = MatchupBlender.Collapse(new[] { legacy }, demNominee: "Kelda Roys", repNominee: "Tom Tiffany");

        var effective = Assert.Single(result);
        Assert.Same(legacy, effective);
    }

    [Fact]
    public void Collapse_NomineeMatching_ToleratesSuffixVariants()
    {
        var result = MatchupBlender.Collapse(new[]
        {
            Poll(44, 48, demCandidate: "Nick Begich III", repCandidate: "Someone Else"),
            Poll(41, 50, demCandidate: "Another Person", repCandidate: "Someone Else"),
        }, demNominee: "Nick Begich");

        var effective = Assert.Single(result);
        Assert.Equal(44, effective.DemPercent);
        Assert.Equal(1, effective.MatchupCount);
    }

    [Fact]
    public void Collapse_NomineeMatching_ToleratesShortFormFirstNames()
    {
        // The ME-02 regression: the roster says "Matthew Dunlap", the poll table says
        // "Matt Dunlap". Containment misses that, and dropping it zeroed the race's average —
        // the loose surname + first-name-prefix pass must keep the poll alive.
        var result = MatchupBlender.Collapse(new[]
        {
            Poll(40, 50, demCandidate: "Matt Dunlap", repCandidate: "Paul LePage"),
        }, demNominee: "Matthew Dunlap", repNominee: "Paul LePage");

        var effective = Assert.Single(result);
        Assert.Equal(40, effective.DemPercent);
    }

    [Fact]
    public void Collapse_NomineeMatching_ToleratesCommonDiminutives()
    {
        // Non-prefix nicknames ("Bob" for "Robert") come from the fixed diminutive table.
        var result = MatchupBlender.Collapse(new[]
        {
            Poll(44, 47, demCandidate: "Bob Casey", repCandidate: "Someone Else"),
            Poll(41, 49, demCandidate: "Another Person", repCandidate: "Someone Else"),
        }, demNominee: "Robert Casey");

        var effective = Assert.Single(result);
        Assert.Equal(44, effective.DemPercent);
        Assert.Equal(1, effective.MatchupCount);
    }

    [Fact]
    public void Collapse_KeepsPollstersAndDatesSeparate()
    {
        var other = new DateTime(2026, 7, 4);
        var result = MatchupBlender.Collapse(new[]
        {
            Poll(47, 42, demCandidate: "Mandela Barnes", repCandidate: "Tom Tiffany"),
            Poll(40, 43, demCandidate: "Francesca Hong", repCandidate: "Tom Tiffany"),
            Poll(48, 44, "Wedgewood", other),
        });

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result.Single(p => p.Pollster == "Marquette").MatchupCount);
        Assert.Equal(1, result.Single(p => p.Pollster == "Wedgewood").MatchupCount);
    }

    [Fact]
    public void Calculate_CountsAMultiMatchupPollOnce()
    {
        var avg = PollingAverageCalculator.Calculate(new List<PollData>
        {
            Poll(47, 42, demCandidate: "Mandela Barnes", repCandidate: "Tom Tiffany"),
            Poll(40, 43, demCandidate: "Francesca Hong", repCandidate: "Tom Tiffany"),
        }, "WI-GOV-2026", PollDate);

        Assert.Equal(1, avg.PollCount);
        // The average must be the cross-matchup mean (D+1), not either published row.
        Assert.Equal(1.0, avg.Margin, 6);
        Assert.Equal(8.0, avg.NomineeSpread, 6);
    }

    [Fact]
    public void Calculate_SpreadIsDecayWeightedAcrossPolls()
    {
        // Stale multi-matchup poll (spread 8) vs a fresh settled poll: the fresh one carries
        // ~4x the weight, so the blended spread sits well below the midpoint — the ambiguity
        // fades as post-primary polling arrives.
        var avg = PollingAverageCalculator.Calculate(new List<PollData>
        {
            Poll(47, 42, date: PollDate.AddDays(-28), demCandidate: "Mandela Barnes", repCandidate: "Tom Tiffany"),
            Poll(40, 43, date: PollDate.AddDays(-28), demCandidate: "Francesca Hong", repCandidate: "Tom Tiffany"),
            Poll(46, 45, "Fresh Poll", PollDate),
        }, "WI-GOV-2026", PollDate);

        Assert.Equal(2, avg.PollCount);
        Assert.True(avg.NomineeSpread > 0);
        Assert.True(avg.NomineeSpread < 2.5);
    }
}
