using ElectionForecaster.Infrastructure.DataSources.Polling;

namespace ElectionForecaster.Tests;

public class PollFilterTests
{
    [Theory]
    [InlineData(48, 46)]  // ordinary close race
    [InlineData(55, 40)]  // ordinary blowout
    [InlineData(25, 41)]  // MT-SEN GrayHouse: high undecideds, both nominees viable
    [InlineData(24, 46)]  // MT-SEN Tavern: trailing nominee in the low 20s but sum 70
    [InlineData(27, 42)]  // VT-GOV UNH
    [InlineData(27, 51)]  // TN-GOV Targoz
    public void KeepsGenuineTwoWayPolls(double dem, double rep)
    {
        Assert.True(PollFilters.IsUsableTwoWay(dem, rep));
    }

    [Theory]
    [InlineData(41, 19)]  // AK-GOV field poll (sum exactly 60 slipped the old < 60 rule)
    [InlineData(38, 22)]  // RI-GOV field poll
    [InlineData(18, 43)]  // SD-SEN: fragmented side under 20 at sum 61
    [InlineData(22, 13)]  // the original Alaska RCV table rows
    [InlineData(50, 14)]  // fragmented side in the teens even at sum 64
    public void RejectsMultiCandidateFieldPolls(double dem, double rep)
    {
        Assert.False(PollFilters.IsUsableTwoWay(dem, rep));
    }

    [Fact]
    public void SumsAboveSeventyFiveNeverNeedTheNomineeFloor()
    {
        // A 15% candidate in a sum-90 poll is a real landslide, not a field artifact
        Assert.True(PollFilters.IsUsableTwoWay(15, 75));
    }
}
