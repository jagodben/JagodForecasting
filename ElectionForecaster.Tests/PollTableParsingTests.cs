using ElectionForecaster.Infrastructure.DataSources.Polling;

namespace ElectionForecaster.Tests;

/// <summary>
/// Pins which Wikipedia poll tables the parser is willing to read. Both cases here were live gaps:
/// Minnesota's every poll was invisible because its Democrats are labelled "(DFL)", and Nebraska's
/// real general-election polling was invisible because Dan Osborn runs as an independent, leaving
/// the table with no "(D)" column at all.
/// </summary>
public class PollTableParsingTests
{
    private static string Table(string demHeader, string repHeader, params string[] rows)
    {
        var body = string.Join("\n", rows);
        return $$"""
        {| class="wikitable"
        |-
        ! Poll source
        ! Date(s)<br />administered
        ! Sample<br />size
        ! Margin<br />of error
        ! {{repHeader}}
        ! {{demHeader}}
        ! Undecided
        {{body}}
        |}
        """;
    }

    private static string Row(string pollster, string date, string sample, string rep, string dem) =>
        $"|-\n|{pollster}\n|{date}\n|{sample}\n|± 3.5%\n|{rep}\n|{dem}\n|5%";

    [Fact]
    public void MinnesotaDflColumnIsReadAsDemocratic()
    {
        var table = Table("Amy Klobuchar (DFL)", "Lisa Demuth (R)",
            Row("Example Poll", "September 8–11, 2026", "800 (LV)", "40%", "52%"));

        var poll = Assert.Single(WikipediaPollingClient.ParseTable(table, "MN-GOV-2026"));
        Assert.Equal(52, poll.DemPercent);
        Assert.Equal(40, poll.RepPercent);
        // The state-party label must not survive into the stored candidate name.
        Assert.Equal("Amy Klobuchar", poll.DemCandidate);
        Assert.Equal("Lisa Demuth", poll.RepCandidate);
    }

    [Fact]
    public void NorthDakotaDemocraticNplColumnIsReadAsDemocratic()
    {
        var table = Table("Jane Doe (D-NPL)", "John Roe (R)",
            Row("Example Poll", "September 8–11, 2026", "600 (LV)", "55%", "41%"));

        var poll = Assert.Single(WikipediaPollingClient.ParseTable(table, "ND-SEN-2026"));
        Assert.Equal(41, poll.DemPercent);
        Assert.Equal("Jane Doe", poll.DemCandidate);
    }

    [Fact]
    public void DesignatedIndependentChallengerIsReadIntoTheChallengerSlot()
    {
        // Nebraska's real general-election table: Ricketts (R) vs Osborn (I), no Democrat.
        var table = Table("Dan Osborn (I)", "Pete Ricketts (R)",
            Row("SurveyUSA", "September 8–13, 2026", "503 (LV)", "42%", "46%"));

        var poll = Assert.Single(WikipediaPollingClient.ParseTable(table, "NE-SEN-2026"));
        Assert.Equal(46, poll.DemPercent);   // the challenger slot carries the independent
        Assert.Equal(42, poll.RepPercent);
        Assert.Equal("Dan Osborn", poll.DemCandidate);
    }

    [Fact]
    public void IndependentColumnIsIgnoredWhereNoneIsDesignated()
    {
        // Michigan's Duggan polls at a real share, but Benson is the Democratic nominee and the
        // model's challenger — an undesignated independent must not hijack the challenger slot.
        var table = Table("Mike Duggan (I)", "John James (R)",
            Row("Example Poll", "September 8–11, 2026", "600 (LV)", "44%", "22%"));

        Assert.Empty(WikipediaPollingClient.ParseTable(table, "MI-GOV-2026"));
    }

    [Fact]
    public void PlainTwoWayTableStillParses()
    {
        var table = Table("Jocelyn Benson (D)", "John James (R)",
            Row("Example Poll", "September 8–11, 2026", "600 (LV)", "45%", "47%"));

        var poll = Assert.Single(WikipediaPollingClient.ParseTable(table, "MI-GOV-2026"));
        Assert.Equal(47, poll.DemPercent);
        Assert.Equal(45, poll.RepPercent);
        Assert.Equal("Jocelyn Benson", poll.DemCandidate);
    }

    [Fact]
    public void AggregatorTablesAreStillSkipped()
    {
        var table = """
        {| class="wikitable"
        |-
        ! Source of poll<br/>aggregation
        ! Dates<br />administered
        ! Amy Klobuchar (DFL)
        ! Lisa Demuth (R)
        |-
        |RealClearPolitics
        |through September 1, 2026
        |52%
        |40%
        |}
        """;

        Assert.Empty(WikipediaPollingClient.ParseTable(table, "MN-GOV-2026"));
    }
}
