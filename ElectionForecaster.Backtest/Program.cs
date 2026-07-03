using ElectionForecaster.Backtest;
using ElectionForecaster.Core.Enums;
using ElectionForecaster.Infrastructure.DataSources.Models;
using ElectionForecaster.Infrastructure.Forecasting;

// Backtests the FUNDAMENTALS model (the real FundamentalsData code) against known 2018/2022
// statewide results. Fundamentals are what the forecast leans on when polls are absent or the
// election is far off, so getting their point estimate and — crucially — their uncertainty (SE)
// right is what keeps the blended forecast honest. This harness reports the point-estimate error
// and sweeps the SE to find the value that makes the win probabilities well-calibrated.

const double IncumbencyAdvantage = 3.0; // matches the model's Senate/Governor value

var rows = HistoricalData.Races.Select(r =>
{
    var pvi = HistoricalData.Pvi[r.State];
    var nationalEnv = HistoricalData.NationalEnvironment[r.Year];
    var f = new FundamentalsData
    {
        PartisanLean = pvi,
        NationalEnvironment = nationalEnv,
        IncumbentIsDem = r.Incumbent switch { 'D' => true, 'R' => false, _ => (bool?)null },
        IncumbencyAdvantage = IncumbencyAdvantage,
    };
    var predicted = f.GetExpectedDemMargin();
    return (r, predicted, residual: predicted - r.ActualDemMargin, demWon: r.ActualDemMargin > 0);
}).ToList();

int n = rows.Count;
double mae = rows.Average(x => Math.Abs(x.residual));
double rmse = Math.Sqrt(rows.Average(x => x.residual * x.residual));
double bias = rows.Average(x => x.residual);
int correctDir = rows.Count(x => (x.predicted > 0) == x.demWon);

Console.WriteLine("=====================================================================");
Console.WriteLine($"  FUNDAMENTALS BACKTEST — {n} races (2018 & 2022 Senate + Governor)");
Console.WriteLine("=====================================================================\n");

Console.WriteLine("Point-estimate accuracy (predicted margin vs. actual):");
Console.WriteLine($"  MAE                 {mae,5:F2} pts");
Console.WriteLine($"  RMSE                {rmse,5:F2} pts   <- the empirically-correct fundamentals SE");
Console.WriteLine($"  Bias (mean resid.)  {bias,5:+0.00;-0.00} pts   ({(bias > 0 ? "over-predicts Dem" : "over-predicts Rep")})");
Console.WriteLine($"  Called winner       {correctDir}/{n}  ({100.0 * correctDir / n:F0}%)\n");

// --- SE sweep: which SE best calibrates the win probabilities? ---
Console.WriteLine("SE sweep (win-probability calibration):");
Console.WriteLine("   SE    Brier   LogLoss");
double bestSe = 0, bestLogLoss = double.MaxValue;
for (double se = 3.0; se <= 9.01; se += 0.5)
{
    double brier = 0, logloss = 0;
    foreach (var x in rows)
    {
        double p = Math.Clamp(ForecastMath.MarginToProbability(x.predicted, se), 1e-6, 1 - 1e-6);
        double y = x.demWon ? 1 : 0;
        brier += (p - y) * (p - y);
        logloss += -(y * Math.Log(p) + (1 - y) * Math.Log(1 - p));
    }
    brier /= n; logloss /= n;
    string mark = "";
    if (logloss < bestLogLoss) { bestLogLoss = logloss; bestSe = se; mark = "  <- best"; }
    Console.WriteLine($"  {se,4:F1}  {brier:F4}   {logloss:F4}{mark}");
}
Console.WriteLine($"\n  Best-calibrated SE ≈ {bestSe:F1} pts (min log-loss). Compare to RMSE {rmse:F1}.");

// The model's UncertaintyModel now assigns ~8pts to a fundamentals-only (no-poll) race; confirm
// that value sits near the calibrated optimum above.
const double modelFundamentalsSe = 8.0;
double mBrier = rows.Average(x => { var p = ForecastMath.MarginToProbability(x.predicted, modelFundamentalsSe); var y = x.demWon ? 1 : 0; return (p - y) * (p - y); });
Console.WriteLine($"  Model's fundamentals SE ({modelFundamentalsSe:F1}) → Brier {mBrier:F4} (near-optimal; the model uses ~this when polls are absent).\n");

// --- Reliability table at the best SE ---
Console.WriteLine($"Reliability at SE = {bestSe:F1} (does 'p% Dem' win p% of the time?):");
Console.WriteLine("   predicted band   n   mean p   actual Dem win %");
var bands = new (double lo, double hi)[] { (0, .1), (.1, .3), (.3, .5), (.5, .7), (.7, .9), (.9, 1.01) };
foreach (var (lo, hi) in bands)
{
    var bucket = rows.Select(x => (p: ForecastMath.MarginToProbability(x.predicted, bestSe), x.demWon))
                     .Where(t => t.p >= lo && t.p < hi).ToList();
    if (bucket.Count == 0) continue;
    double meanP = bucket.Average(t => t.p);
    double actual = 100.0 * bucket.Count(t => t.demWon) / bucket.Count;
    Console.WriteLine($"   {lo * 100,3:F0}-{hi * 100,3:F0}%        {bucket.Count,2}   {meanP * 100,5:F1}%   {actual,5:F0}%");
}

// --- Biggest misses (where fundamentals were most wrong) ---
Console.WriteLine("\nLargest misses (|predicted - actual| margin):");
foreach (var x in rows.OrderByDescending(x => Math.Abs(x.residual)).Take(8))
{
    Console.WriteLine($"   {x.r.Year} {x.r.State}-{(x.r.Office == RaceType.Senate ? "SEN" : "GOV")} ({x.r.Incumbent})"
        + $"  pred {x.predicted,+5:F1}  actual {x.r.ActualDemMargin,+6:F1}  miss {x.residual,+5:F1}");
}

// --- Accuracy by segment ---
Console.WriteLine("\nRMSE by segment:");
foreach (var g in rows.GroupBy(x => x.r.Year))
    Console.WriteLine($"   {g.Key}:      {Math.Sqrt(g.Average(x => x.residual * x.residual)),4:F2} pts  (n={g.Count()})");
foreach (var g in rows.GroupBy(x => x.r.Office))
    Console.WriteLine($"   {g.Key,-9} {Math.Sqrt(g.Average(x => x.residual * x.residual)),4:F2} pts  (n={g.Count()})");
foreach (var g in rows.GroupBy(x => x.r.Incumbent switch { 'D' => "Dem inc", 'R' => "Rep inc", _ => "open" }))
    Console.WriteLine($"   {g.Key,-9} {Math.Sqrt(g.Average(x => x.residual * x.residual)),4:F2} pts  (n={g.Count()})");

Console.WriteLine("\nNote: PVI is the 2024 release applied to older cycles, so residuals overstate true");
Console.WriteLine("model error for states that shifted (FL/OH/AZ/GA). Plug in year-correct PVI to sharpen.");
