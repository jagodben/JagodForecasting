using ElectionForecaster.Core.Enums;
using ElectionForecaster.Infrastructure.DataSources.Models;
using ElectionForecaster.Infrastructure.Forecasting;
using Microsoft.Extensions.Configuration;

namespace ElectionForecaster.Tests;

public class WeightCalculatorTests
{
    private static readonly DateTime AsOf = new(2026, 7, 15);

    private static WeightCalculator Calc() =>
        new(new ConfigurationBuilder().Build());

    private static MarketOdds Market(double volume = 500_000) => new()
    {
        RaceId = "X", Source = "Polymarket", DemOdds = 0.6, RepOdds = 0.4,
        Timestamp = AsOf, Volume = volume,
    };

    private static PollingAverage Polls(int count = 6, double confidence = 0.7) => new()
    {
        RaceId = "X", DemPercent = 48, RepPercent = 46, PollCount = count, Confidence = confidence,
    };

    private static FundamentalsData Fundamentals() => new() { RaceId = "X", PartisanLean = 2 };

    [Fact]
    public void WeightsAlwaysSumToOne()
    {
        var full = Calc().CalculateWeights(Market(), Polls(), Fundamentals(), RaceType.Senate, AsOf);
        var noMarket = Calc().CalculateWeights(null, Polls(), Fundamentals(), RaceType.Senate, AsOf);
        var bare = Calc().CalculateWeights(null, null, Fundamentals(), RaceType.Senate, AsOf);

        foreach (var w in new[] { full, noMarket, bare })
            Assert.Equal(1.0, w.MarketWeight + w.PollingWeight + w.FundamentalsWeight, 6);
    }

    [Fact]
    public void MissingSourcesGetZeroWeight()
    {
        var noMarket = Calc().CalculateWeights(null, Polls(), Fundamentals(), RaceType.Senate, AsOf);
        Assert.Equal(0, noMarket.MarketWeight, 6);

        var noPolls = Calc().CalculateWeights(Market(), null, Fundamentals(), RaceType.Senate, AsOf);
        Assert.Equal(0, noPolls.PollingWeight, 6);
    }

    [Fact]
    public void PollingWeightScalesWithConfidence()
    {
        var shaky = Calc().CalculateWeights(Market(), Polls(count: 1, confidence: 0.3), Fundamentals(), RaceType.Senate, AsOf);
        var deep = Calc().CalculateWeights(Market(), Polls(count: 10, confidence: 0.9), Fundamentals(), RaceType.Senate, AsOf);
        Assert.True(deep.PollingWeight > shaky.PollingWeight);
    }

    [Fact]
    public void ThinPollingKeepsFundamentalsLateInTheCycle()
    {
        // ~6 weeks out, where the late-cycle haircut applies. A two-poll field must not outvote a
        // seat's partisan history the way a deep field legitimately can (the Idaho case: two polls
        // briefly put an independent at 76% in a seat the Republicans last won by 29).
        var lateOn = new DateTime(2026, 9, 19);
        var thin = Calc().CalculateWeights(Market(), Polls(count: 2, confidence: 0.46), Fundamentals(), RaceType.Senate, lateOn);
        var deep = Calc().CalculateWeights(Market(), Polls(count: 15, confidence: 0.85), Fundamentals(), RaceType.Senate, lateOn);

        Assert.True(thin.FundamentalsWeight > deep.FundamentalsWeight);
        Assert.True(thin.FundamentalsWeight > thin.PollingWeight);
        // A deep field still gets to lead — the haircut isn't disabled, just conditioned.
        Assert.True(deep.PollingWeight > deep.FundamentalsWeight);
    }

    [Fact]
    public void ThinMarketsCarryLessWeight()
    {
        var liquid = Calc().CalculateWeights(Market(volume: 2_000_000), Polls(), Fundamentals(), RaceType.Senate, AsOf);
        var thin = Calc().CalculateWeights(Market(volume: 500), Polls(), Fundamentals(), RaceType.Senate, AsOf);
        Assert.True(liquid.MarketWeight > thin.MarketWeight);
    }

    [Fact]
    public void MarketsFadeAndPollsRiseAsElectionNears()
    {
        var electionDay = new DateTime(2026, 11, 3);
        var farOut = Calc().CalculateWeights(Market(), Polls(), Fundamentals(), RaceType.Senate, electionDay.AddDays(-200));
        var midCycle = Calc().CalculateWeights(Market(), Polls(), Fundamentals(), RaceType.Senate, electionDay.AddDays(-100));
        var closing = Calc().CalculateWeights(Market(), Polls(), Fundamentals(), RaceType.Senate, electionDay.AddDays(-7));

        Assert.True(farOut.MarketWeight > midCycle.MarketWeight);
        Assert.True(midCycle.MarketWeight > closing.MarketWeight);
        Assert.True(farOut.PollingWeight < midCycle.PollingWeight);
        Assert.True(midCycle.PollingWeight < closing.PollingWeight);
    }
}
