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
        double nationalSwing = SampleTError(natSD);

        // A second correlated swing per Census region, drawn once and shared by every race in that
        // region, so a polling miss in one Midwest state partly carries to the others (but not to the
        // West). This sits between the fully-shared national swing and the independent race error, and
        // it's what most tightens the chamber odds — clustered upsets move seat totals together.
        double regSD = UncertaintyModel.RegionalErrorStdDev;
        var regionalSwings = new Dictionary<string, double>();

        foreach (var forecast in forecasts)
        {
            var raceSD = forecast.MarginStdDev > 0 ? forecast.MarginStdDev : 6.0;

            var region = GetRegion(forecast.RaceId);
            if (!regionalSwings.TryGetValue(region, out var regionalSwing))
            {
                regionalSwing = SampleTError(regSD);
                regionalSwings[region] = regionalSwing;
            }

            // Decompose each race's total uncertainty into the shared national and regional
            // components plus an independent race-specific remainder, so total per-race variance
            // still ≈ raceSD² (natSD² + regSD² + idioSD²).
            var idioSD = Math.Sqrt(Math.Max(raceSD * raceSD - natSD * natSD - regSD * regSD, 1.0));
            var simMargin = forecast.ExpectedDemMargin + nationalSwing + regionalSwing + SampleTError(idioSD);

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

    // Degrees of freedom for the t-distributed error terms. Fewer dof = fatter tails; ~5 reflects
    // that real polling misses have heavier tails than a normal, so the sim stops underpricing big
    // swings and overstating near-certain outcomes.
    private const int ErrorDof = 5;

    private double SampleStandardNormal()
    {
        // Box-Muller transform, mean 0 / SD 1.
        double u1 = 1.0 - _random.NextDouble();
        double u2 = 1.0 - _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    /// <summary>
    /// Mean-0 error with standard deviation <paramref name="stdDev"/>, drawn from a Student-t with
    /// <see cref="ErrorDof"/> degrees of freedom. t = Z / sqrt(W/dof) with W ~ chi-squared(dof); a
    /// standard t has variance dof/(dof-2), so rescale by sqrt((dof-2)/dof) to unit variance before
    /// scaling to <paramref name="stdDev"/> — same spread as the old normal, but with fatter tails.
    /// </summary>
    private double SampleTError(double stdDev)
    {
        double z = SampleStandardNormal();
        double chiSq = 0;
        for (int i = 0; i < ErrorDof; i++)
        {
            double n = SampleStandardNormal();
            chiSq += n * n;
        }
        double standardT = z / Math.Sqrt(chiSq / ErrorDof);
        return stdDev * standardT * Math.Sqrt((ErrorDof - 2.0) / ErrorDof);
    }

    // Census regions — the correlation clusters for the regional swing. Coarse enough that every race
    // lands in a group, fine enough that the Midwest and the South miss in different directions. Any
    // state not listed (e.g. DC) falls through to its own singleton bucket, which is harmless.
    private static readonly Dictionary<string, string> StateToRegion = new()
    {
        // Northeast
        ["CT"] = "NE", ["ME"] = "NE", ["MA"] = "NE", ["NH"] = "NE", ["RI"] = "NE", ["VT"] = "NE",
        ["NJ"] = "NE", ["NY"] = "NE", ["PA"] = "NE",
        // Midwest
        ["IL"] = "MW", ["IN"] = "MW", ["MI"] = "MW", ["OH"] = "MW", ["WI"] = "MW", ["IA"] = "MW",
        ["KS"] = "MW", ["MN"] = "MW", ["MO"] = "MW", ["NE"] = "MW", ["ND"] = "MW", ["SD"] = "MW",
        // South
        ["DE"] = "S", ["FL"] = "S", ["GA"] = "S", ["MD"] = "S", ["NC"] = "S", ["SC"] = "S",
        ["VA"] = "S", ["WV"] = "S", ["DC"] = "S", ["AL"] = "S", ["KY"] = "S", ["MS"] = "S",
        ["TN"] = "S", ["AR"] = "S", ["LA"] = "S", ["OK"] = "S", ["TX"] = "S",
        // West
        ["AZ"] = "W", ["CO"] = "W", ["ID"] = "W", ["MT"] = "W", ["NV"] = "W", ["NM"] = "W",
        ["UT"] = "W", ["WY"] = "W", ["AK"] = "W", ["CA"] = "W", ["HI"] = "W", ["OR"] = "W",
        ["WA"] = "W",
    };

    /// <summary>
    /// Maps a race to its regional correlation bucket via the state prefix of its id (e.g.
    /// "OH-SEN-2026" or "CA-01-2026" → the state code before the first '-'). Unknown states get
    /// their own bucket keyed by the state code, so they simply don't correlate with anything else.
    /// </summary>
    private static string GetRegion(string raceId)
    {
        if (string.IsNullOrEmpty(raceId)) return "?";
        var dash = raceId.IndexOf('-');
        var state = dash > 0 ? raceId[..dash] : raceId;
        return StateToRegion.TryGetValue(state, out var region) ? region : state;
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
