using ElectionForecaster.Core.Enums;
using ElectionForecaster.Infrastructure.DataSources.Models;
using ElectionForecaster.Infrastructure.Forecasting;

namespace ElectionForecaster.Tests;

public class UncertaintyModelTests
{
    [Fact]
    public void UncertaintyShrinksAsElectionNears()
    {
        var farOut = UncertaintyModel.MarginStandardError(200, RaceType.Senate, 0);
        var months = UncertaintyModel.MarginStandardError(90, RaceType.Senate, 0);
        var finalWeek = UncertaintyModel.MarginStandardError(5, RaceType.Senate, 0);

        Assert.True(farOut > months);
        Assert.True(months > finalWeek);
    }

    [Fact]
    public void PollingReducesUncertaintyWithDiminishingReturns()
    {
        var none = UncertaintyModel.MarginStandardError(90, RaceType.Senate, 0);
        var three = UncertaintyModel.MarginStandardError(90, RaceType.Senate, 3);
        var ten = UncertaintyModel.MarginStandardError(90, RaceType.Senate, 10);

        Assert.True(three < none);
        Assert.True(ten < three);
        // Cube-root scaling: polls 4-10 together help less than polls 1-3
        Assert.True(none - three > three - ten);
    }

    [Fact]
    public void GovernorsAreNoisierThanSenate()
    {
        var senate = UncertaintyModel.MarginStandardError(90, RaceType.Senate, 5);
        var governor = UncertaintyModel.MarginStandardError(90, RaceType.Governor, 5);
        Assert.True(governor > senate);
    }

    [Fact]
    public void NoRaceIsEverACertainty()
    {
        // Even a heavily polled race on election eve keeps the floor SD
        var se = UncertaintyModel.MarginStandardError(1, RaceType.Senate, 50);
        Assert.True(se >= 3.5);
    }
}

public class FundamentalsTests
{
    [Fact]
    public void IncumbencyPointsTowardTheIncumbentsParty()
    {
        var demSeat = new FundamentalsData { PartisanLean = 0, NationalEnvironment = 0, IncumbentIsDem = true, IncumbencyAdvantage = 3 };
        var repSeat = new FundamentalsData { PartisanLean = 0, NationalEnvironment = 0, IncumbentIsDem = false, IncumbencyAdvantage = 3 };
        var open = new FundamentalsData { PartisanLean = 0, NationalEnvironment = 0, IncumbentIsDem = null, IncumbencyAdvantage = 3 };

        Assert.Equal(3, demSeat.GetExpectedDemMargin(), 6);
        Assert.Equal(-3, repSeat.GetExpectedDemMargin(), 6);
        Assert.Equal(0, open.GetExpectedDemMargin(), 6);
    }

    [Fact]
    public void CrossoverIncumbentRetainsPartOfPastOverperformance()
    {
        // A Republican who won a D+10 state (think Vermont's governor): PVI says D+10,
        // but the prior result must drag the expectation strongly toward the incumbent.
        var crossover = new FundamentalsData
        {
            PartisanLean = 10,
            NationalEnvironment = 0,
            IncumbentIsDem = false,
            IncumbencyAdvantage = 3,
            PriorMargin = -25,
        };
        var pviOnly = new FundamentalsData
        {
            PartisanLean = 10,
            NationalEnvironment = 0,
            IncumbentIsDem = false,
            IncumbencyAdvantage = 3,
        };

        Assert.True(crossover.GetExpectedDemMargin() < pviOnly.GetExpectedDemMargin());
        // Retention is partial: it must not fully reproduce the past blowout either
        Assert.True(crossover.GetExpectedDemMargin() > -25);
    }

    [Fact]
    public void OpenSeatsIgnoreTheDepartingIncumbentsPrior()
    {
        var open = new FundamentalsData
        {
            PartisanLean = 10,
            NationalEnvironment = 0,
            IncumbentIsDem = null,
            IncumbencyAdvantage = 3,
            PriorMargin = -25,
        };
        // The personal vote left with the incumbent — only the lean remains
        Assert.Equal(10, open.GetExpectedDemMargin(), 6);
    }

    [Fact]
    public void ExtremePriorsAreClamped()
    {
        var landslide = new FundamentalsData
        {
            PartisanLean = 30,
            NationalEnvironment = 10,
            IncumbentIsDem = true,
            IncumbencyAdvantage = 5,
            PriorMargin = 70,
        };
        Assert.True(Math.Abs(landslide.GetExpectedDemMargin()) <= 40);
    }
}
