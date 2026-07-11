# Jagod Forecasting — 2026 Midterm Election Model

A forecasting model and interactive site for the 2026 U.S. midterm elections, live at
[jagodforecasting.com](https://jagodforecasting.com). It covers all 35 Senate races
(33 Class-2 seats plus the Florida and Ohio specials), all 435 House districts on the
lines actually in use for 2026, and all 36 governorships.

The model blends three independent signals — prediction markets, polling, and
structural fundamentals — into a win probability for every race, then runs a
10,000-iteration Monte Carlo over each chamber to estimate control odds and seat
distributions.

## The site

- **Dashboard** — Senate, House, and Governors maps colored by the blended forecast,
  with chamber win probability, projected seats, and a control-probability timeline.
- **House map on real 2026 lines** — including the ten states redrawn mid-decade
  (AL, CA, FL, LA, MO, NC, OH, TN, TX, UT).
- **Race pages** — per-race probabilities, the model's inputs and weights, the polls
  feeding the average, and a prediction-over-time chart.
- **State pages** — every race in a state plus its congressional district grid.

Every surface reads from the same blended model, so the map, tooltips, sidebars, and
race pages always agree.

## How the model works

### Three signals

**Prediction markets.** Per-race "which party wins" markets from Polymarket, refreshed
every 15 minutes. Odds are volume-weighted and each market's influence scales with its
liquidity. Races without a usable party market (Alaska's ranked-choice contests,
California's top-two governor race) fall back to the other signals.

**Polling.** General-election polls parsed from each race's Wikipedia article,
deduplicated across hypothetical-matchup tables. The average weights each poll by
recency (14-day half-life), sample size, likely-voter screen, and a curated
pollster-quality tier; partisan-sponsored polls carry half weight. Pollster house
effects are estimated from each firm's deviation against the leave-one-out consensus
on the races it polled, shrunk toward zero, and subtracted before averaging. Polls are
blended on a two-party margin so they sit on the same scale as the other signals.

**Fundamentals.** For every race: 2025 Cook PVI (on 2026 district lines), the national
environment from the generic-ballot average, a flat incumbency term, and the seat's
prior election result. A running incumbent keeps 35% of their past overperformance
beyond the flat incumbency term — this is what lets the model see a crossover incumbent
like Vermont's Phil Scott instead of assuming the seat votes its PVI. Open seats drop
the prior entirely: the personal vote leaves with the departing incumbent. Both rules
were chosen by backtest, not judgment (see Validation).

### Blending

Each signal is expressed as an expected Democratic margin and combined with weights
that shift by data availability, time to election, and race type (baseline: polling
0.45, fundamentals 0.40, markets 0.15). Two safeguards:

- **Market-disagreement guard** — when a liquid market diverges from the
  poll+fundamentals blend by more than 10 points of implied margin, the market's
  weight ramps up (capped at 0.70). Markets price candidate-specific reality the
  structural model can't see.
- **Independent challengers** — where a viable independent is the real challenger
  (Nebraska's Dan Osborn), the model prices them through the market and their own
  demonstrated prior, and drops D-vs-R polling that only measures a token nominee.

### Uncertainty

The blended margin becomes a probability through a fat-tailed Student-t distribution —
the same one the simulator draws its errors from, so per-race probabilities and the
chamber simulation describe one distribution. The margin's standard error shrinks as
the election approaches and as polling accumulates (with cube-root diminishing
returns), never below 3.5 points; governor races carry extra variance (crossover
incumbents), House races slightly more than Senate.

### Chamber simulation

Each chamber forecast is a 10,000-iteration Monte Carlo. Every iteration draws one
national swing (SD 3), one swing per Census region (SD 2), and an independent
race-specific error, all from fat-tailed t(5) distributions, then counts seats. The
shared swings are what make the control odds honest: correlated polling misses move
many seats together, so the seat distribution is much wider than independent races
would suggest. Senate control accounts for the 65 seats not up in 2026 (34 D / 31 R)
and the Republican tie-breaking vice presidency.

"Projected seats" is the average across simulations — close races count fractionally
toward both parties, which is why it can differ from tallying each race's current
leader on the map.

## Validation

The fundamentals pipeline is backtested against 82 known 2018/2022 Senate and Governor
results (`ElectionForecaster.Backtest`), including a fidelity check that the scored
formula is byte-identical to the live code. The live configuration beat both the
flat-incumbency model and naive prior-retention on every metric (MAE 6.08 vs 6.24,
RMSE 8.06 vs 8.42, Brier 0.138 vs 0.149), driven by crossover-incumbent seats
(2018 West Virginia error: 14.7 → 0.7 points). The backtest also *rejected* two ideas
that sounded reasonable — open-seat prior retention and environment-adjusted priors —
both of which scored worse; neither is in the model.

## Data sources and update cadence

| Input | Source | Cadence |
|---|---|---|
| Market odds | Polymarket (Gamma + CLOB APIs) | 15 min |
| Polls | Wikipedia race articles | 6 h |
| Generic ballot | Wikipedia aggregator table | 6 h |
| District PVI / lines | 2025 Cook PVI on 2026 lines; Census + state GIS geometry | static, scripted refresh |
| Prior results | Real 2020–2024 results per seat | static, scripted refresh |
| Nominees & incumbents | Wikipedia race-summary scrapes | after each primary |

Daily snapshots persist every race's forecast and each chamber's simulation, which is
what the prediction-over-time charts plot. The `tools/` scripts regenerate the static
tables (district PVI, 2024 results, statewide incumbents, map geometry) from their
sources.

## Known limitations

- No candidate-quality inputs beyond incumbency and the seat's own history — no
  fundraising, scandals, or approval ratings.
- The national environment applies uniformly; states don't yet have elasticity.
- House districts are essentially unpolled, so House rests on fundamentals plus the
  generic ballot — the chamber's headline number is only as good as that environment
  estimate.
- Several constants (blend weights, incumbency sizes, swing SDs) are calibrated
  judgment, validated only where the backtest reaches.

## Tech

ASP.NET Core 8 API (three-layer: Core / Infrastructure / Api) with a SQLite store,
React 19 + TypeScript + Vite frontend, d3-geo for the maps. To run locally:

```bash
dotnet run --project ElectionForecaster.Api        # API on :5000
cd election-forecaster-client && npm install && npm run dev   # site on :5173
```

## License

MIT
