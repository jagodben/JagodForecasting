using ElectionForecaster.Infrastructure.DataSources.Models;
using ElectionForecaster.Infrastructure.DataSources.Polling;

namespace ElectionForecaster.Tests;

/// <summary>
/// Pins the undecided-primary matchup blending: one row per (pollster, date) whose display
/// percentages come from the first-listed matchup while the model sees the cross-matchup mean,
/// with the margin spread between matchups feeding the nominee-uncertainty SE term.
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
    public void Blend_SingleMatchup_PassesThroughUnchanged()
    {
        var result = MatchupBlender.Blend(new[] { (Poll(47, 42), 0) });

        var poll = Assert.Single(result);
        Assert.Equal(47, poll.DemPercent);
        Assert.Equal(42, poll.RepPercent);
        Assert.Null(poll.BlendDemPercent);
        Assert.Equal(1, poll.MatchupCount);
        Assert.Equal(0, poll.MatchupSpread, 6);
        Assert.Equal(47, poll.ModelDemPercent);
    }

    [Fact]
    public void Blend_MultipleMatchups_AveragesForModelKeepsFirstForDisplay()
    {
        // The WI-GOV shape: Barnes–Tiffany D+5 listed first, Hong–Tiffany R+3 further down.
        var result = MatchupBlender.Blend(new[]
        {
            (Poll(47, 42), 0), // Barnes v Tiffany
            (Poll(40, 43), 3), // Hong v Tiffany
        });

        var poll = Assert.Single(result);
        // Display: the first-listed matchup, matching the published poll.
        Assert.Equal(47, poll.DemPercent);
        Assert.Equal(42, poll.RepPercent);
        // Model: the mean across tested matchups.
        Assert.Equal(43.5, poll.ModelDemPercent, 6);
        Assert.Equal(42.5, poll.ModelRepPercent, 6);
        Assert.Equal(2, poll.MatchupCount);
        // Spread: D+5 down to R+3 = 8 points of nominee dependence.
        Assert.Equal(8, poll.MatchupSpread, 6);
    }

    [Fact]
    public void Blend_LvAndRvRowsInOneTable_KeepFirstOnly()
    {
        // Within a single matchup table the same pollster/date repeats (LV line, then RV line);
        // only the first line counts, and it is not treated as a second matchup.
        var result = MatchupBlender.Blend(new[]
        {
            (Poll(47, 42), 0), // LV
            (Poll(44, 40), 0), // RV
        });

        var poll = Assert.Single(result);
        Assert.Equal(47, poll.DemPercent);
        Assert.Equal(1, poll.MatchupCount);
        Assert.Equal(0, poll.MatchupSpread, 6);
    }

    [Fact]
    public void Blend_KeepsPollstersAndDatesSeparate()
    {
        var other = new DateTime(2026, 7, 4);
        var result = MatchupBlender.Blend(new[]
        {
            (Poll(47, 42), 0),
            (Poll(40, 43), 1),
            (Poll(48, 44, "Wedgewood", other), 0),
        });

        Assert.Equal(2, result.Count);
        var marquette = result.Single(p => p.Pollster == "Marquette");
        var wedgewood = result.Single(p => p.Pollster == "Wedgewood");
        Assert.Equal(2, marquette.MatchupCount);
        Assert.Equal(1, wedgewood.MatchupCount);
        Assert.Equal(0, wedgewood.MatchupSpread, 6);
    }

    [Fact]
    public void Blend_SettledNominees_CollapseToTheirMatchup()
    {
        // Once Hong wins the primary, the Barnes table is a stale hypothetical: the poll must
        // collapse to the Hong–Tiffany numbers — display and model alike, no blend, no spread.
        var result = MatchupBlender.Blend(new[]
        {
            (Poll(47, 42, demCandidate: "Mandela Barnes", repCandidate: "Tom Tiffany"), 0),
            (Poll(40, 43, demCandidate: "Francesca Hong", repCandidate: "Tom Tiffany"), 3),
        }, demNominee: "Francesca Hong", repNominee: "Tom Tiffany");

        var poll = Assert.Single(result);
        Assert.Equal(40, poll.DemPercent);
        Assert.Equal(43, poll.RepPercent);
        Assert.Null(poll.BlendDemPercent);
        Assert.Equal(1, poll.MatchupCount);
        Assert.Equal(0, poll.MatchupSpread, 6);
    }

    [Fact]
    public void Blend_OneSideSettled_BlendsOnlyThatSidesMatchups()
    {
        // Rep primary decided (Tiffany), Dem still open: the Michels table drops out; the
        // remaining Tiffany matchups still blend with their spread.
        var result = MatchupBlender.Blend(new[]
        {
            (Poll(47, 42, demCandidate: "Mandela Barnes", repCandidate: "Tom Tiffany"), 0),
            (Poll(40, 43, demCandidate: "Francesca Hong", repCandidate: "Tom Tiffany"), 1),
            (Poll(49, 40, demCandidate: "Mandela Barnes", repCandidate: "Tim Michels"), 2),
        }, repNominee: "Tom Tiffany");

        var poll = Assert.Single(result);
        Assert.Equal(2, poll.MatchupCount);
        Assert.Equal(43.5, poll.ModelDemPercent, 6);
        Assert.Equal(8, poll.MatchupSpread, 6);
    }

    [Fact]
    public void Blend_NomineeNeverPolled_FallsBackToFullBlend()
    {
        // The settled nominee has no head-to-head table: averaging what exists beats a blind pick.
        var result = MatchupBlender.Blend(new[]
        {
            (Poll(47, 42, demCandidate: "Mandela Barnes", repCandidate: "Tom Tiffany"), 0),
            (Poll(40, 43, demCandidate: "Francesca Hong", repCandidate: "Tom Tiffany"), 1),
        }, demNominee: "Kelda Roys");

        var poll = Assert.Single(result);
        Assert.Equal(2, poll.MatchupCount);
        Assert.Equal(43.5, poll.ModelDemPercent, 6);
    }

    [Fact]
    public void Blend_NomineeMatching_ToleratesSuffixVariants()
    {
        var result = MatchupBlender.Blend(new[]
        {
            (Poll(44, 48, demCandidate: "Nick Begich III", repCandidate: "Someone Else"), 0),
            (Poll(41, 50, demCandidate: "Another Person", repCandidate: "Someone Else"), 1),
        }, demNominee: "Nick Begich");

        var poll = Assert.Single(result);
        Assert.Equal(44, poll.DemPercent);
        Assert.Equal(1, poll.MatchupCount);
    }

    [Fact]
    public void Calculate_UsesBlendedPercentagesAndCarriesSpread()
    {
        var poll = Poll(47, 42);
        poll.BlendDemPercent = 43.5;
        poll.BlendRepPercent = 42.5;
        poll.MatchupCount = 2;
        poll.MatchupSpread = 8;

        var avg = PollingAverageCalculator.Calculate(new List<PollData> { poll }, "WI-GOV-2026", PollDate);

        // The average must reflect the blend (D+1), not the displayed first matchup (D+5).
        Assert.Equal(1.0, avg.Margin, 6);
        Assert.Equal(8.0, avg.NomineeSpread, 6);
    }

    [Fact]
    public void Calculate_SpreadIsDecayWeightedAcrossPolls()
    {
        var ambiguous = Poll(47, 42, date: PollDate.AddDays(-28)); // stale multi-matchup poll
        ambiguous.MatchupSpread = 8;
        var settled = Poll(46, 45, "Fresh Poll", PollDate); // fresh single-matchup poll

        var avg = PollingAverageCalculator.Calculate(
            new List<PollData> { ambiguous, settled }, "WI-GOV-2026", PollDate);

        // The fresh settled poll carries ~4x the weight, so the blended spread sits well
        // below the midpoint — the ambiguity fades as post-primary polling arrives.
        Assert.True(avg.NomineeSpread > 0);
        Assert.True(avg.NomineeSpread < 2.5);
    }
}
