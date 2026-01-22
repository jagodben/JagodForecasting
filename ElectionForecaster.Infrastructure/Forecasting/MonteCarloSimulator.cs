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

    // Current seat baselines (seats not up for election in 2026)
    // Senate: 34 Class 2 seats up in 2026
    // Democrats have some seats not up that cycle
    private const int SenateDemBaseline = 23; // Dem seats not up in 2026 (Class 1 + Class 3)
    private const int SenateRepBaseline = 43; // Rep seats not up in 2026

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

        // Add correlation between races (national swing)
        // This prevents unrealistic scenarios where all close races break one way
        double nationalSwing = SampleNationalSwing();

        foreach (var forecast in forecasts)
        {
            // Adjust probability based on national swing
            var adjustedDemProb = AdjustProbability(forecast.DemWinProbability, nationalSwing);

            // Simulate race outcome
            if (_random.NextDouble() < adjustedDemProb)
            {
                demSeats++;
            }
            else
            {
                repSeats++;
            }
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

    private double SampleNationalSwing()
    {
        // National swing follows approximately normal distribution
        // Standard deviation of ~2-3 points is typical
        return SampleNormal(0, 0.025); // Mean 0, StdDev 2.5%
    }

    private double AdjustProbability(double baseProb, double swing)
    {
        // Convert probability to margin, apply swing, convert back
        // Using logit transform for better behavior at extremes
        double logit = Math.Log(baseProb / (1 - baseProb));
        double adjustedLogit = logit + swing * 4; // Scale swing to logit space
        double adjustedProb = 1 / (1 + Math.Exp(-adjustedLogit));

        // Ensure valid probability
        return Math.Max(0.001, Math.Min(0.999, adjustedProb));
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
