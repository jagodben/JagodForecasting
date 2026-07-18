using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Models;
using ElectionForecaster.Infrastructure.Forecasting;

namespace ElectionForecaster.Tests;

public class ChamberSimulationTests
{
    private static List<DetailedForecast> SenateField(double demMargin, double sd = 4)
        => Enumerable.Range(0, 35).Select(i => new DetailedForecast
        {
            RaceId = $"S{i:D2}-SEN-2026",
            ExpectedDemMargin = demMargin,
            MarginStdDev = sd,
            DemWinProbability = ForecastMath.MarginToProbability(demMargin, sd),
        }).ToList();

    [Fact]
    public void SenateSeatsAlwaysSumToOneHundred()
    {
        var sim = new MonteCarloSimulator();
        var result = sim.SimulateChamber(SenateField(1.5), RaceType.Senate, iterations: 2000);
        // 34 D + 31 R not up, plus the 35 modeled races
        Assert.Equal(100, result.ExpectedDemSeats + result.ExpectedRepSeats, 3);
    }

    [Fact]
    public void SafeSeatsProduceNearCertainControl()
    {
        var sim = new MonteCarloSimulator();
        var demSweep = sim.SimulateChamber(SenateField(25, 3), RaceType.Senate, iterations: 2000);
        var repSweep = sim.SimulateChamber(SenateField(-25, 3), RaceType.Senate, iterations: 2000);

        Assert.True(demSweep.DemControlProbability > 0.99);
        Assert.True(repSweep.DemControlProbability < 0.01);
    }

    [Fact]
    public void ProbabilitiesAreComplementaryAndBounded()
    {
        var sim = new MonteCarloSimulator();
        var result = sim.SimulateChamber(SenateField(0.5, 6), RaceType.Senate, iterations: 2000);

        Assert.Equal(1.0, result.DemControlProbability + result.RepControlProbability, 6);
        Assert.InRange(result.DemControlProbability, 0.02, 0.98);
        Assert.True(result.DemSeatRange.Low <= result.ExpectedDemSeats);
        Assert.True(result.DemSeatRange.High >= result.ExpectedDemSeats);
    }

    [Fact]
    public void CorrelatedErrorsWidenTheSeatRange()
    {
        // With national/regional swings shared across races, tail outcomes must be wider
        // than an independent-races binomial would allow. 35 tossups, p=0.5: an independent
        // binomial's 10th-90th percentile spans ~8 seats; correlated swings stretch it well past that.
        var sim = new MonteCarloSimulator();
        var result = sim.SimulateChamber(SenateField(0, 6), RaceType.Senate, iterations: 4000);
        Assert.True(result.DemSeatRange.High - result.DemSeatRange.Low > 8);
    }

    [Fact]
    public void HouseCountsOnlyModeledRaces()
    {
        var sim = new MonteCarloSimulator();
        var races = Enumerable.Range(0, 20).Select(i => new DetailedForecast
        {
            RaceId = $"H{i:D2}-{i + 1:D2}-2026",
            ExpectedDemMargin = 10,
            MarginStdDev = 4,
            DemWinProbability = 0.9,
        }).ToList();

        var result = sim.SimulateChamber(races, RaceType.House, iterations: 500);
        Assert.Equal(20, result.ExpectedDemSeats + result.ExpectedRepSeats, 3);
    }
}
