using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Models;

namespace ElectionForecaster.Infrastructure.Forecasting;

/// <summary>
/// Performs Monte Carlo simulations to estimate chamber control probabilities.
/// </summary>
public class MonteCarloSimulator
{
    private readonly Random _random = new();
    private const int DefaultIterations = 10000;

    // Chamber thresholds for control
    private const int SenateControlThreshold = 51; // 51 seats needed for control
    private const int HouseControlThreshold = 218; // 218 seats needed for control

    // Seat baselines: seats NOT up in 2026, by current party. The app models 33 Class 2 races plus
    // the FL and OH 2026 specials (appointed R incumbents) = 35 Senate races, so baseline + 35 = 100.
    // Starting from the post-2024 Senate (53R / 47D incl. 2 independents), the 33 Class 2 seats are
    // 13 Dem-held / 20 Rep-held; removing the two now-modeled Rep specials from the not-up pool
    // leaves 34 Dem / 31 Rep.
    private const int SenateDemBaseline = 34;
    private const int SenateRepBaseline = 31;

    // House: All 435 seats up every 2 years
    private const int HouseTotalSeats = 435;

    public ChamberForecast SimulateChamber(
        List<DetailedForecast> raceForecasts,
        RaceType chamber,
        int iterations = DefaultIterations)
    {
        var results = new List<SimulationResult>();

        for (int i = 0; i < iterations; i++)
        {
            var result = SimulateSingleOutcome(raceForecasts, chamber);
            results.Add(result);
        }

        return AggregateResults(results, chamber, iterations);
    }

    private SimulationResult SimulateSingleOutcome(List<DetailedForecast> forecasts, RaceType chamber)
    {
        int demSeats = chamber == RaceType.Senate ? SenateDemBaseline : 0;
        int repSeats = chamber == RaceType.Senate ? SenateRepBaseline : 0;

        // One correlated national swing per simulation (margin points), shared across all races —
        // this is what prevents unrealistic scenarios where every close race breaks the same way.
        double natSD = UncertaintyModel.NationalErrorStdDev;
        double nationalSwing = SampleNormal(0, natSD);

        foreach (var forecast in forecasts)
        {
            var raceSD = forecast.MarginStdDev > 0 ? forecast.MarginStdDev : 6.0;

            // Decompose each race's total uncertainty into the shared national component plus an
            // independent race-specific component, so total per-race variance ≈ raceSD².
            var idioSD = Math.Sqrt(Math.Max(raceSD * raceSD - natSD * natSD, 1.0));
            var simMargin = forecast.ExpectedDemMargin + nationalSwing + SampleNormal(0, idioSD);

            if (simMargin > 0) demSeats++;
            else repSeats++;
        }

        return new SimulationResult
        {
            DemSeats = demSeats,
            RepSeats = repSeats,
            DemControl = chamber == RaceType.Senate
                ? demSeats >= SenateControlThreshold
                : demSeats >= HouseControlThreshold
        };
    }

    private double SampleNormal(double mean, double stdDev)
    {
        // Box-Muller transform for normal distribution
        double u1 = 1.0 - _random.NextDouble();
        double u2 = 1.0 - _random.NextDouble();
        double normalRandom = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + stdDev * normalRandom;
    }

    private ChamberForecast AggregateResults(
        List<SimulationResult> results,
        RaceType chamber,
        int iterations)
    {
        var demSeats = results.Select(r => r.DemSeats).OrderBy(s => s).ToList();
        var demWins = results.Count(r => r.DemControl);

        var controlThreshold = chamber == RaceType.Senate
            ? SenateControlThreshold
            : HouseControlThreshold;

        return new ChamberForecast
        {
            Chamber = chamber.ToString(),
            DemControlProbability = (double)demWins / iterations,
            RepControlProbability = 1.0 - ((double)demWins / iterations),
            ExpectedDemSeats = results.Average(r => r.DemSeats),
            ExpectedRepSeats = results.Average(r => r.RepSeats),
            SeatsNeededForControl = controlThreshold,
            LastUpdated = DateTime.UtcNow,
            DemSeatRange = new SeatRange
            {
                Low = demSeats[(int)(iterations * 0.10)],  // 10th percentile
                High = demSeats[(int)(iterations * 0.90)], // 90th percentile
                Median = demSeats[iterations / 2]
            },
            SimulationIterations = iterations
        };
    }

    private class SimulationResult
    {
        public int DemSeats { get; set; }
        public int RepSeats { get; set; }
        public bool DemControl { get; set; }
    }
}
