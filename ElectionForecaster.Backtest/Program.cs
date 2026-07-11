using ElectionForecaster.Backtest;
using ElectionForecaster.Core.Enums;
using ElectionForecaster.Infrastructure.DataSources.Models;
using ElectionForecaster.Infrastructure.Forecasting;

// Backtests the FULL fundamentals pipeline — PVI + national environment + seat-level prior
// results with mean-reversion retention (the live model since the VT-governor fix) — against
// known 2018/2022 statewide outcomes. The point: the retention constants (0.45 incumbent /
// 0.25 open) were judgment calls; this scores them against reality, compares the prior-using
// model to the flat-incumbency model it replaced, and tests an environment-adjusted variant
// (subtracting the prior cycle's national mood from the seat's overperformance).

const double IncumbencyAdvantage = 3.0; // the live statewide value
const double MaxMargin = 40.0;          // live clamp

var races = HistoricalData.Races;

// ---------- 0. Priors sanity: prior winner should match a running incumbent's party ----------
Console.WriteLine("=====================================================================");
Console.WriteLine($"  FULL-PIPELINE BACKTEST — {races.Count} races (2018 & 2022 Senate + Governor)");
Console.WriteLine("=====================================================================\n");

int priorCount = 0, missing = 0;
foreach (var r in races)
{
    var p = HistoricalPriors.Get(r);
    if (p is null) { missing++; Console.WriteLine($"  NOTE: no prior on file for {r.Year} {r.State}-{r.Office}"); continue; }
    priorCount++;
    // A running incumbent's party should normally have won the seat's prior contest.
    if (r.Incumbent == 'D' && p.Value.DemMargin < 0)
        Console.WriteLine($"  CHECK: {r.Year} {r.State}-{r.Office}: D incumbent but prior was R+{-p.Value.DemMargin:F1}");
    if (r.Incumbent == 'R' && p.Value.DemMargin > 0)
        Console.WriteLine($"  CHECK: {r.Year} {r.State}-{r.Office}: R incumbent but prior was D+{p.Value.DemMargin:F1}");
}
Console.WriteLine($"Priors on file: {priorCount}/{races.Count} ({missing} missing)\n");

// ---------- helpers ----------
double PredictNoPrior(HistoricalRace r)
{
    var margin = HistoricalData.Pvi[r.State] + HistoricalData.NationalEnvironment[r.Year];
    if (r.Incumbent == 'D') margin += IncumbencyAdvantage;
    else if (r.Incumbent == 'R') margin -= IncumbencyAdvantage;
    return margin;
}

double PredictWithPrior(HistoricalRace r, double rInc, double rOpen, bool envAdjusted)
{
    var p = HistoricalPriors.Get(r);
    if (p is null) return PredictNoPrior(r);
    var pvi = HistoricalData.Pvi[r.State];
    var env = HistoricalData.NationalEnvironment[r.Year];
    var retention = r.Incumbent == 'O' ? rOpen : rInc;
    var over = p.Value.DemMargin - pvi;
    if (envAdjusted) over -= HistoricalPriors.CycleEnvironment[p.Value.PriorYear];
    return Math.Clamp(pvi + env + retention * over, -MaxMargin, MaxMargin);
}

// Hybrid: keep the flat incumbency term and retain only the overperformance BEYOND it —
// nests the no-prior model at r=0 and approaches the raw-prior model as r→1.
double PredictHybrid(HistoricalRace r, double rInc)
{
    var p = HistoricalPriors.Get(r);
    var pvi = HistoricalData.Pvi[r.State];
    var env = HistoricalData.NationalEnvironment[r.Year];
    double incSign = r.Incumbent == 'D' ? 1 : r.Incumbent == 'R' ? -1 : 0;
    if (p is null || r.Incumbent == 'O')
        return pvi + env + incSign * IncumbencyAdvantage; // open seats: prior dropped entirely
    var overExcess = p.Value.DemMargin - pvi - incSign * IncumbencyAdvantage;
    return Math.Clamp(pvi + env + incSign * IncumbencyAdvantage + rInc * overExcess, -MaxMargin, MaxMargin);
}

(double mae, double rmse, int called) Score(Func<HistoricalRace, double> predict)
{
    double sae = 0, sse = 0; int called = 0;
    foreach (var r in races)
    {
        var resid = predict(r) - r.ActualDemMargin;
        sae += Math.Abs(resid); sse += resid * resid;
        if ((predict(r) > 0) == (r.ActualDemMargin > 0)) called++;
    }
    return (sae / races.Count, Math.Sqrt(sse / races.Count), called);
}

(double brier, double logloss) ScoreProb(Func<HistoricalRace, double> predict, double se)
{
    double b = 0, ll = 0;
    foreach (var r in races)
    {
        var p = Math.Clamp(ForecastMath.MarginToProbability(predict(r), se), 1e-6, 1 - 1e-6);
        double y = r.ActualDemMargin > 0 ? 1 : 0;
        b += (p - y) * (p - y);
        ll += -(y * Math.Log(p) + (1 - y) * Math.Log(1 - p));
    }
    return (b / races.Count, ll / races.Count);
}

double BestSe(Func<HistoricalRace, double> predict)
{
    double best = 8, bestLl = double.MaxValue;
    for (double se = 3; se <= 12.01; se += 0.5)
    {
        var (_, ll) = ScoreProb(predict, se);
        if (ll < bestLl) { bestLl = ll; best = se; }
    }
    return best;
}

// ---------- 1. Fidelity: the local formula must reproduce the LIVE FundamentalsData ----------
int fidelityFails = 0;
foreach (var r in races)
{
    var p = HistoricalPriors.Get(r);
    var f = new FundamentalsData
    {
        PartisanLean = HistoricalData.Pvi[r.State],
        NationalEnvironment = HistoricalData.NationalEnvironment[r.Year],
        IncumbentIsDem = r.Incumbent switch { 'D' => true, 'R' => false, _ => (bool?)null },
        IncumbencyAdvantage = IncumbencyAdvantage,
        PriorMargin = p?.DemMargin
    };
    var live = f.GetExpectedDemMargin();
    var local = PredictHybrid(r, 0.35); // the live model: hybrid form, 0.35 excess retention
    if (Math.Abs(live - local) > 1e-9) fidelityFails++;
}
Console.WriteLine(fidelityFails == 0
    ? "Fidelity check: hybrid(0.35) == live FundamentalsData for all races. OK\n"
    : $"Fidelity check FAILED for {fidelityFails} races — sweep results would not describe the live model!\n");

// ---------- 2. Headline comparison ----------
var variants = new (string Name, Func<HistoricalRace, double> Predict)[]
{
    ("A. No priors (flat incumbency — pre-fix model)", PredictNoPrior),
    ("B. Raw priors, first-cut constants (0.45 / 0.25)", r => PredictWithPrior(r, 0.45, 0.25, false)),
    ("C. Priors, env-adjusted (0.45 / 0.25)", r => PredictWithPrior(r, 0.45, 0.25, true)),
    ("D. Raw priors, open-seat retention dropped (0.45/0)", r => PredictWithPrior(r, 0.45, 0.0, false)),
    ("E. Hybrid: flat inc + 0.35 x excess, open dropped  <- LIVE", r => PredictHybrid(r, 0.35)),
};

Console.WriteLine("Variant                                              MAE   RMSE  called   Brier* logloss*  (SE*)");
foreach (var (name, predict) in variants)
{
    var (mae, rmse, called) = Score(predict);
    var se = BestSe(predict);
    var (brier, ll) = ScoreProb(predict, se);
    Console.WriteLine($"{name,-52} {mae,5:F2} {rmse,6:F2}  {called,3}/{races.Count}   {brier:F4}  {ll:F4}   ({se:F1})");
}
Console.WriteLine("* at that variant's best-calibrated SE\n");

// ---------- 2b. Hybrid retention sweep ----------
Console.WriteLine("Hybrid sweep (flat incumbency + r x excess overperformance; open seats drop the prior):");
Console.WriteLine("   r     MAE   RMSE  called  logloss@bestSE");
for (double ri = 0.0; ri <= 0.81; ri += 0.10)
{
    var predict = (Func<HistoricalRace, double>)(r => PredictHybrid(r, ri));
    var (mae, rmse, called) = Score(predict);
    var (_, ll) = ScoreProb(predict, BestSe(predict));
    Console.WriteLine($"  {ri:F2}  {mae,5:F2} {rmse,6:F2}  {called,3}/{races.Count}   {ll:F4}");
}
Console.WriteLine();

// ---------- 3. Retention sweep (RMSE of predicted margin; SE-independent) ----------
foreach (var envAdj in new[] { false, true })
{
    Console.WriteLine($"Retention sweep — {(envAdj ? "ENV-ADJUSTED" : "RAW")} priors (cell = margin RMSE):");
    var opens = new[] { 0.0, 0.10, 0.25, 0.45 };
    Console.Write("  rInc\\rOpen ");
    foreach (var ro in opens) Console.Write($"{ro,7:F2}");
    Console.WriteLine();
    double bestRmse = double.MaxValue; (double ri, double ro) best = (0, 0);
    for (double ri = 0.0; ri <= 0.801; ri += 0.10)
    {
        Console.Write($"  {ri,8:F2}  ");
        foreach (var ro in opens)
        {
            var (_, rmse, _) = Score(r => PredictWithPrior(r, ri, ro, envAdj));
            if (rmse < bestRmse) { bestRmse = rmse; best = (ri, ro); }
            Console.Write($"{rmse,7:F2}");
        }
        Console.WriteLine();
    }
    Console.WriteLine($"  best: rInc={best.ri:F2}, rOpen={best.ro:F2} -> RMSE {bestRmse:F2}\n");
}

// ---------- 4. Where the priors help and hurt most (LIVE hybrid vs no-prior) ----------
Console.WriteLine("Largest changes from adding priors (live hybrid model), by |error| improvement:");
var deltas = races
    .Select(r =>
    {
        var errA = Math.Abs(PredictNoPrior(r) - r.ActualDemMargin);
        var errB = Math.Abs(PredictHybrid(r, 0.35) - r.ActualDemMargin);
        return (r, errA, errB, delta: errA - errB);
    })
    .OrderByDescending(x => Math.Abs(x.delta))
    .Take(10);
foreach (var (r, errA, errB, delta) in deltas)
    Console.WriteLine($"   {r.Year} {r.State}-{(r.Office == RaceType.Senate ? "SEN" : "GOV")} ({r.Incumbent})"
        + $"  err {errA,5:F1} -> {errB,5:F1}  ({(delta >= 0 ? "improved" : "WORSE"),-8} {Math.Abs(delta):F1})");

// ---------- 5. Reliability of the live model at its best SE ----------
{
    var predict = (Func<HistoricalRace, double>)(r => PredictHybrid(r, 0.35));
    var se = BestSe(predict);
    Console.WriteLine($"\nReliability of the live model at SE={se:F1} (does 'p% Dem' win p% of the time?):");
    Console.WriteLine("   predicted band   n   mean p   actual Dem win %");
    var bands = new (double lo, double hi)[] { (0, .1), (.1, .3), (.3, .5), (.5, .7), (.7, .9), (.9, 1.01) };
    foreach (var (lo, hi) in bands)
    {
        var bucket = races
            .Select(r => (p: ForecastMath.MarginToProbability(predict(r), se), won: r.ActualDemMargin > 0))
            .Where(t => t.p >= lo && t.p < hi).ToList();
        if (bucket.Count == 0) continue;
        Console.WriteLine($"   {lo * 100,3:F0}-{hi * 100,3:F0}%        {bucket.Count,2}   {bucket.Average(t => t.p) * 100,5:F1}%   {100.0 * bucket.Count(t => t.won) / bucket.Count,5:F0}%");
    }
}

Console.WriteLine("\nNote: PVI is the 2024 release applied to older cycles, so residuals overstate true");
Console.WriteLine("model error for states that shifted (FL/OH/AZ/GA). Priors are curated to ~1-2 pts.");
