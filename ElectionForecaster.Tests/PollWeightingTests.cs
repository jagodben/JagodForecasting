using ElectionForecaster.Infrastructure.DataSources.Models;
using ElectionForecaster.Infrastructure.DataSources.Polling;

namespace ElectionForecaster.Tests;

public class PollWeightingTests
{
    private static readonly DateTime AsOf = new(2026, 7, 15);

    private static PollData Poll(int daysOld = 0, int? sample = 800, string? population = "LV",
        string? methodology = null, double dem = 48, double rep = 46, string pollster = "Test Poll")
        => new()
        {
            RaceId = "XX-SEN-2026",
            Pollster = pollster,
            Date = AsOf.AddDays(-daysOld),
            SampleSize = sample,
            Population = population,
            Methodology = methodology,
            DemPercent = dem,
            RepPercent = rep,
        };

    [Fact]
    public void GetWeight_HalvesEveryFourteenDays()
    {
        var fresh = Poll(daysOld: 0).GetWeight(AsOf);
        var twoWeeks = Poll(daysOld: 14).GetWeight(AsOf);
        var fourWeeks = Poll(daysOld: 28).GetWeight(AsOf);

        Assert.Equal(0.5, twoWeeks / fresh, 6);
        Assert.Equal(0.25, fourWeeks / fresh, 6);
        // Old polls are dampened, never zeroed
        Assert.True(Poll(daysOld: 80).GetWeight(AsOf) > 0);
    }

    [Fact]
    public void GetWeight_PartisanPollsCountHalf()
    {
        var independent = Poll().GetWeight(AsOf);
        var partisan = Poll(methodology: "Partisan (R)").GetWeight(AsOf);
        Assert.Equal(0.5, partisan / independent, 6);
    }

    [Fact]
    public void GetWeight_LikelyVotersBeatAdults()
    {
        var lv = Poll(population: "LV").GetWeight(AsOf);
        var rv = Poll(population: "RV").GetWeight(AsOf);
        var adults = Poll(population: "A").GetWeight(AsOf);
        Assert.True(lv > rv);
        Assert.True(rv > adults);
    }

    [Fact]
    public void Calculate_WeightsRecentPollsHigher()
    {
        // Old poll says D+10, fresh poll says R+2: the average must sit closer to the fresh one
        var polls = new List<PollData>
        {
            Poll(daysOld: 60, dem: 53, rep: 43),
            Poll(daysOld: 1, dem: 47, rep: 49),
        };
        var avg = PollingAverageCalculator.Calculate(polls, "XX-SEN-2026", AsOf);
        Assert.True(avg.Margin < 0);
    }

    [Fact]
    public void Calculate_AppliesHouseEffectDebiasing()
    {
        var polls = new List<PollData> { Poll(dem: 50, rep: 44) };
        var neutral = PollingAverageCalculator.Calculate(polls, "XX-SEN-2026", AsOf);
        // This pollster leans D+4, so the corrected margin should come in 4 points lower
        var debiased = PollingAverageCalculator.Calculate(polls, "XX-SEN-2026", AsOf,
            new Dictionary<string, double> { ["Test Poll"] = 4.0 });

        Assert.Equal(6.0, neutral.Margin, 6);
        Assert.Equal(2.0, debiased.Margin, 6);
        // De-biasing shifts the margin, not the two-party total
        Assert.Equal(neutral.DemPercent + neutral.RepPercent, debiased.DemPercent + debiased.RepPercent, 6);
    }

    [Fact]
    public void Calculate_ConfidenceGrowsWithPollCount()
    {
        var one = PollingAverageCalculator.Calculate(new List<PollData> { Poll() }, "X", AsOf);
        var six = PollingAverageCalculator.Calculate(
            Enumerable.Range(0, 6).Select(i => Poll(daysOld: i)).ToList(), "X", AsOf);

        Assert.True(six.Confidence > one.Confidence);
        // A single poll must read as fragile, well below the ceiling
        Assert.True(one.Confidence < 0.6);
        Assert.True(six.Confidence <= 1.0);
    }

    [Fact]
    public void Calculate_EmptyInputYieldsEmptyAverage()
    {
        var avg = PollingAverageCalculator.Calculate(new List<PollData>(), "X", AsOf);
        Assert.Equal(0, avg.PollCount);
        Assert.Equal(0, avg.Margin, 6);
    }

    [Fact]
    public void TwoPartyMargin_RescalesUndecideds()
    {
        var avg = new PollingAverage { DemPercent = 45, RepPercent = 43 };
        // D45/R43 with 12% undecided: raw margin +2, two-party margin +2.27
        Assert.Equal(2.0, avg.Margin, 6);
        Assert.Equal(200.0 / 88.0, avg.TwoPartyMargin, 6);
    }
}
