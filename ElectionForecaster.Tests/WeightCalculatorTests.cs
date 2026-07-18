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
    public void ThinMarketsCarryLessWeight()
    {
        var liquid = Calc().CalculateWeights(Market(volume: 2_000_000), Polls(), Fundamentals(), RaceType.Senate, AsOf);
        var thin = Calc().CalculateWeights(Market(volume: 500), Polls(), Fundamentals(), RaceType.Senate, AsOf);
        Assert.True(liquid.MarketWeight > thin.MarketWeight);
    }
}
