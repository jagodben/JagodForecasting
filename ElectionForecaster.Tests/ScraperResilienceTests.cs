using ElectionForecaster.Api.Services;
using ElectionForecaster.Infrastructure.DataSources;
using ElectionForecaster.Infrastructure.DataSources.Polling;

namespace ElectionForecaster.Tests;

/// <summary>
/// Pins the scrapers against the Wikipedia layout changes that silently froze data in Sept 2026:
/// a renamed generic-ballot section, California's House page split into sub-articles, and a
/// general-election "Polling" section retitled "Post-primary polling". Plus the staleness check
/// that would have caught each of them within a day.
/// </summary>
public class ScraperResilienceTests
{
    private const string BallotTable = """
        {| class="wikitable"
        |-
        ! Source of poll<br/>aggregation
        ! Dates<br/>administered
        ! Dates<br/>updated
        ! Republicans
        ! Democrats
        ! Other/<br/>Undecided
        ! Margin
        |-
        |Decision Desk HQ
        |January 9, 2025 – October 5, 2026
        |October 9, 2026
        |40.3%
        |48.8%
        |10.9%
        |Democrats +8.5%
        |-
        |FiftyPlusOne
        |January 9, 2025 – October 5, 2026
        |October 9, 2026
        |43.3%
        |51.1%
        |5.6%
        |Democrats +7.8%
        |}
        """;

    private const string SeatsTable = """
        {| class="wikitable"
        |-
        ! Party
        ! Seats
        |-
        |Democrats
        |213
        |}
        """;

    [Theory]
    [InlineData("Generic congressional ballot aggregate polls")]
    [InlineData("Opinion polling")]
    public void BallotTableIsFoundByShapeUnderAnyHeading(string heading)
    {
        // An unrelated table comes first, as on the real page; the heading text doesn't matter.
        var page = $"== Results ==\n{SeatsTable}\n== {heading} ==\n{BallotTable}";

        var parsed = WikipediaGenericBallotClient.ParseAggregate(page);

        Assert.NotNull(parsed);
        Assert.Equal(2, parsed.Value.Count);
        Assert.Equal(49.95, parsed.Value.DemPercent, 6);
        Assert.Equal(41.8, parsed.Value.RepPercent, 6);
    }

    [Fact]
    public void PageWithoutAnAggregationTableYieldsNothing()
    {
        Assert.Null(WikipediaGenericBallotClient.ParseAggregate($"== Results ==\n{SeatsTable}"));
    }

    [Theory]
    [InlineData(1, "2026 United States House of Representatives elections in California (districts 1–26)")]
    [InlineData(13, "2026 United States House of Representatives elections in California (districts 1–26)")]
    [InlineData(27, "2026 United States House of Representatives elections in California (districts 27–52)")]
    [InlineData(52, "2026 United States House of Representatives elections in California (districts 27–52)")]
    [InlineData(53, null)]
    public void SplitStatePageLeadsToTheDistrictsSubArticle(int district, string? expected)
    {
        const string parent = """
            ==Districts 1–26==
            {{main|2026 United States House of Representatives elections in California (districts 1–26)}}

            ==Districts 27–52==
            {{Main|2026 United States House of Representatives elections in California (districts 27–52)}}
            """;

        Assert.Equal(expected, WikipediaSplitArticles.SubArticleForDistrict(parent, district));
    }

    [Fact]
    public void UnsplitStatePageHasNoSubArticle()
    {
        Assert.Null(WikipediaSplitArticles.SubArticleForDistrict("==District 1==\nsome text", 1));
    }

    [Fact]
    public void EveryGeneralElectionPollingSubsectionIsRead()
    {
        const string generalElection = """
            === Predictions ===
            ratings
            === Post-primary polling ===
            post-primary table
            ==== Hypothetical polling ====
            nested table
            === Results ===
            results table
            === Aggregate polls ===
            aggregate table
            """;

        var block = WikipediaPollingClient.ExtractSectionBlocks(
            generalElection, t => t.Contains("poll", StringComparison.OrdinalIgnoreCase), 3);

        Assert.NotNull(block);
        Assert.Contains("post-primary table", block);
        Assert.Contains("nested table", block);
        Assert.Contains("aggregate table", block);
        Assert.DoesNotContain("ratings", block);
        Assert.DoesNotContain("results table", block);
    }

    private static readonly DateTime Now = new(2026, 10, 11, 14, 0, 0);

    [Fact]
    public void FreshFeedsReportNothingStale()
    {
        var stale = DataFreshness.StaleSources(Now, Now.Date, Now.Date, new[]
        {
            ("CA-13-2026", Now.AddHours(-6)),
            ("CA-GOV-2026", Now.AddHours(-6)),
        });

        Assert.Empty(stale);
    }

    [Fact]
    public void FrozenGenericBallotIsReported()
    {
        var stale = DataFreshness.StaleSources(Now, Now.Date, new DateTime(2026, 9, 16),
            Array.Empty<(string, DateTime)>());

        Assert.Equal("generic ballot (latest 2026-09-16)", Assert.Single(stale));
    }

    [Fact]
    public void AStatePageWhereNothingRefreshedIsReported()
    {
        var stale = DataFreshness.StaleSources(Now, Now.Date, Now.Date, new[]
        {
            // California's split page: no district refreshed in three weeks.
            ("CA-01-2026", new DateTime(2026, 9, 20)),
            ("CA-13-2026", new DateTime(2026, 9, 20)),
            // Louisiana: LA-06's infobox lists no nominee yet, but the page itself is fine.
            ("LA-01-2026", Now.AddHours(-6)),
            ("LA-06-2026", new DateTime(2026, 7, 21)),
            // A statewide article has its own page, judged separately from its state's House page.
            ("CA-GOV-2026", Now.AddHours(-6)),
        });

        Assert.Equal("candidates: CA House (last refreshed 2026-09-20)", Assert.Single(stale));
    }
}
