# Todo

## 📌 FIX LIST — everything open, ordered by importance (2026-07-09)

Every unfinished item across all reviews/audits, ranked. Work top to bottom.

### 1. ✅ DONE — Redraw the House data layer on 2026 district lines
The redraw wave turned out to be TEN states (AL, CA, FL, LA, MO, NC, OH, TN, TX, UT — 181
districts, 42% of the House), and the old `DistrictPVI` table was wrong far beyond them
(298/435 districts off by ≥3 pts, e.g. GA-06 off by 33). Fixed:
- **PVI**: replaced all 435 entries with the 2025 Cook PVI on current 2026 lines, scraped from
  Wikipedia's "2026 United States House of Representatives elections" page (which carries the
  post-redistricting values). Regenerate with `tools/scrape_district_pvi.py`.
- **Priors**: deleted `Results2024` rows for the 10 redrawn states (results earned on lines
  that no longer exist); kept the other 254.
- **Geometry**: rebuilt `districts.json` from the Census CD119 base + each redrawn state's
  officially published plan (TX PLANC2333, NC SL 2025-95, MO HB1, AL's certified 2023 plan,
  CA Prop 50/AB 604, OH 2025-11-03, UT 2026-2032, FL May-2026, TN, LA — all state GIS sources).
  Rebuild with `tools/build_districts_topojson.sh`; feature property is now `DISTRICT`
  (was `CD118FP`), object `districts_2026`.
- **Verified**: TN-09 sprawls 3.8° (Memphis cracked) vs 0.7° before; UT-01 is the new SLC seat
  (97.6% D); AL-02 Figures now an underdog (44.7% D) in his R+7 seat; GA-06 corrected to safe D;
  House sim totals exactly 435; map + state pages render the new lines.

### 2. ✅ DONE — StatePage now serves the blended model
Extracted the overlay into a shared `ForecastOverlay` helper (Api/Services); `StatesController`
now applies it to `/api/states/{id}` (races AND the district-grid ratings/house races) and
`/api/states/{id}/races`, with per-race fallback to the baseline on forecast failure. Verified
OH: state page Senate probability (0.4945) and district-grid ratings match `/forecast` exactly.

### 3. ✅ DONE — House seat-level priors (with a rebuilt Results2024)
The old `Results2024` table turned out to be as fabricated as the old PVI: 187/254 kept rows
were off by ≥3 pts and **14 districts had the wrong 2024 winner** (also corrupting the
incumbency fallback for unresolved districts). Fixed:
- Rebuilt all 254 un-redrawn rows with real 2024 results scraped from Wikipedia's 2024 House
  elections page (`tools/scrape_house_results.py`; winner cross-checked against the {{Aye}}
  marker; one-party races get a ±45 placeholder). Redrawn states stay absent.
- `CookPVIProvider` now feeds the district's real 2024 Dem margin into
  `FundamentalsData.PriorMargin` for House races (redrawn states stay on PVI + incumbency).
- Prior retention is now open-seat aware (0.45 when the incumbent runs again, 0.25 when the
  personal vote leaves with a departing incumbent) — applies to statewide races too, so e.g.
  term-limited governors' landslides no longer carry at full weight. Placeholder-nominee races
  are treated as open until their primaries resolve (conservative, self-correcting).
- Verified both retention paths to the decimal (IA-01 incumbent, ME-02 open); House chamber
  expectation tempered 256.9 → 250.4 D; statewide history re-backfilled on the new methodology.
- FOLLOW-UP DONE: the House sidebar now runs off the simulation like the Senate one (control
  probability + expected seats + a Race Timeline). The House control line is rebuilt from the
  stored daily generic-ballot series (`BackfillHouseChamberHistoryAsync`) — the House model's
  only time-varying input — so a rebuild also purges rows computed by older code/data; the
  daily snapshot extends it going forward.

### 4. Hard 90-day poll window silently blinds stale-but-polled races
MD-GOV (Oct 2025), AL-GOV, IL-GOV, KY-SEN have only pre-2026 polls, so their forecasts run
market+fundamentals only. The 14-day half-life already down-weights old polls smoothly.
**Fix:** widen the window to ~1 year and let the decay do the work (a 6-month-old poll beats
no poll).

### 5. Refresh House nominees after the summer primaries *(time-sensitive, recurring)*
360/435 districts resolved as of 2026-07-07. Re-run the House-discovery scraper after each
primary night — AZ Jul 21, MI Aug 4, WY Aug 18, NH Sep 8, … — to fill the ~75 placeholder
districts and replace presumptive nominees. Also re-run `ElectionForecaster.MarketDiscovery`
occasionally to catch newly opened Polymarket markets.

### 6. Remove or redefine the state "overall rating" badge *(misleading UI)*
`StateService.UpdateStateRating` computes it as state PVI + a flat 4 Dem bonus — it isn't a
forecast of any race, yet it's the big colored badge atop StatePage. **Fix:** replace with
per-race rating chips or drop it.

### 7. District endpoints serve fabricated ratings *(public API correctness)*
`/api/states/{id}/districts` and `/api/districts/{id}` (DistrictService) build their own
`ElectionDataProvider.GetAllStates()` copy whose ratings come from `GetDistrictRating`
(state rating + districtNumber % 3 − 1 — a fake pattern). The frontend no longer calls these,
but they're public API. **Fix:** wire them to real forecasts or delete them (and delete
`GetDistrictRating` + the unused `_random` while there).

### 8. House district landing pages *(feature — completes the map's click-through)*
Senate/Governor map clicks navigate to a race page; House district clicks only fill the mobile
info panel. **Fix:** wire `USDistrictMap.handleDistrictClick` to `/race/{stateId}-{DD}-2026`
(RacePage already handles House race ids); check the layout reads well for a district
(leading-zero district number, at-large = 01).

### 9. Deduplicate `probabilityToRating` / rating colors *(consistency risk)*
The same thresholds+palette exist in RaceMap, USDistrictMap, StateMap, USMap (legacy, unused —
delete it), RacesController, and RaceService. Exactly 0.50 currently renders Tilt R.
**Fix:** one TS module + one C# helper; define the 0.50 convention once.

### 10. Market weight: revisit empirically *(ongoing tuning)*
The 15% default is a documented judgment call; once more history accumulates, compare the model
line vs. the stored market line per race and tune `Forecasting:DefaultWeights` (already
configurable). The disagreement guard covers the extreme cases meanwhile.

### 11. Small data nits
- WV-SEN prior listed as −35; actual 2020 was ~R+43 (Capito) — understated (cap is ±45).
- VT-GOV prior is −49 but the file's doc comment says values are capped at ±45 — make the doc
  and data agree.

### 12. `StateService` constructor blocks on `.Result` *(hygiene)*
`raceService.GetAllRacesAsync().Result` is sync-over-async in DI construction; fine today
(in-memory) but a deadlock risk if the service ever becomes truly async.

## 🔴 Critical — Vermont-governor class of failures (from 2026-07-09 VT-GOV diagnosis)

Symptom: VT-GOV shows ~90% Dem while Polymarket has Scott (R) at 89.5% and the only
poll (UNH, June 23) has him +15. Two independent causes, both verified against forecast.db:

- [x] **Statewide fundamentals ignore prior election results / who the candidates are.**
      `FundamentalsData` for Senate/Gov is just `state PVI + national environment ± flat 3` —
      for VT-GOV that's D+16 + D+5.8 − 3 ≈ D+18.8 → a 98.5% Dem "fundamentals prediction"
      for a race whose incumbent (Phil Scott) won 2024 by ~51 points. House fundamentals
      already blend in 2024 results; statewide doesn't. Fix: add a prior-race-margin term for
      statewide races — when the incumbent is running, blend the last election margin for this
      seat (shrunk ~40–50% toward PVI, since personal landslides mean-revert) with meaningful
      weight; decay it heavily for open seats. Needs a small static table of 2020/2022/2024
      statewide results (Senate seats' last cycle + each governorship's last race).
      DONE (7707f7e): new `StatewidePriorResults` table (last Dem margin per modeled Sen/Gov seat);
      `GetExpectedDemMargin` = PVI + national + 0.45*(prior − PVI), clamped ±40. Drives off the
      static table not the (placeholder) candidate flags, since most nominees aren't set pre-primary.
      VT-GOV fundamentals 0.993 → 0.199, blend ~7% Dem. House unchanged (no prior entry).

- [x] **A failed/empty Wikipedia poll fetch poisons the blend even when polls exist in the DB.**
      In `WikipediaPollingClient.GetRacePollsAsync`, the DB fallback only fires on *exception*.
      A MediaWiki error body (rate-limit/maxlag) returns 200 with no `parse` property →
      `FetchAndParseAsync` returns an empty list → cached for 6h → PollingWeight=0, and the
      startup backfill bakes zero-poll rows into ForecastHistory. That's exactly VT-GOV's state
      (poll in DB, PollingAverage=None in every history row). Fix: (a) treat a missing `parse`
      property as a failure, (b) on an empty parse result, fall back to (or union with) the
      DB polls, (c) don't cache an empty result for the full 6h TTL.
      DONE (f9214e4): missing `parse` now throws (→ DB fallback); an empty parse result falls back to
      persisted DB polls; empty results cache for only `EmptyCacheTtl` (20 min). VT-GOV now shows
      pollCount=1 in the blend and history.

- [x] **Add a market-disagreement guard.** When a liquid market and the poll/fundamentals blend
      imply margins that differ wildly (say >10 pts), the market almost certainly knows something
      the structural model can't see (candidate popularity, scandal, crossover incumbent). Boost
      the market weight toward dominance as disagreement grows (or at minimum log/flag the race).
      This is the generic safety net for every future VT-like case.
      DONE (7e1a889): `ApplyMarketDisagreementGuard` ramps market weight from base to a 0.70 cap as
      the market-vs-structural gap grows past 10 pts (liquidity floor 0.5; logs each engagement).
      Post-fundamentals-fix it rarely touches competitive races; mostly regularizes overconfident
      safe-seat fundamentals toward the market's calibrated extremes.

- [x] **After the above: re-run `POST /api/forecast/backfill`** so the poisoned zero-poll history
      rows for VT-GOV (and any other affected races) are rebuilt with polls included.
      DONE: forced backfill re-run (35s). VT-GOV history rebuilt from a flat ~92% Dem to a coherent
      ~25% (Jun 1) → ~7% (Jul) decline as the June 23 poll + market get folded in.

## 🔴 Critical — model correctness (from 2026-07-07 model review)

- [x] **Fix House fundamentals: use the real district PVI table in the live model.** (e7716a7)
      Done. Root cause was actually two bugs: (1) `GetFundamentalsAsync` mis-parsed the House
      race ID "CA-01-2026" and read "2026" as the district number, so district PVI was NEVER used
      — it always fell back to state PVI + the `(districtNumber % 5) - 2` wobble; (2) the local
      ~48-district table had the opposite sign convention. Now parses the real ID formats and
      reads `DistrictElectionData.DistrictPVI` (negated). Verified CA-01 flips fabricated D+17.8 →
      real R-lean; all 435 House forecasts present.

- [x] **Make the map/ratings show the blended model, not the static baseline.** (303e496)
      Done. `RacesController` now overlays the orchestrator's blended `DetailedForecast` onto each
      race's `Rating` + candidate win probabilities (non-mutating; falls back to the baseline when
      a forecast isn't cached yet). Verified `/api/races` demWinProbability matches `/forecast`
      exactly for all 33 Senate + 435 House races. RaceService math kept only as the cold-start
      placeholder — its incumbency-double-count / bare-midterm-term flaws now only surface for the
      brief cold-cache window (could still delete it later per the note).

- [x] **Stop the startup backfill from wiping real forecast history.** (7c63590)
      Done. `BackfillModelHistoryAsync` now takes `force`; the automatic startup path runs only
      when `ForecastHistory` is empty (first-time seed), so restarts no longer delete/rebuild real
      snapshots. The admin endpoint `POST /api/forecast/backfill` passes `force: true` to rebuild
      deliberately. Verified: restart logs "skipping automatic backfill" and history is preserved.

- [x] **House chamber sim: don't silently drop failed races.** (8c011e2)
      Done. `SimulateChamberAsync` now reconciles the batch forecasts against the full chamber race
      list and backfills any missing race from its RaceService prior (correct lean, default SE),
      logging when it does. Verified the sim totals exactly 435 House / 100 Senate seats.

- [x] **Model the FL and OH 2026 Senate specials as real races.** (9b98904)
      Done. Enabled FL/OH Senate races (flagged `IsSpecialElection`), added FL's appointed
      incumbent Moody to `SenateNominees`, mapped their "Will the Democrats win…" Polymarket
      markets (FL 631044 / OH 631057), and dropped `SenateRepBaseline` 33→31. Verified the sim
      still totals 100 across 35 races; Senate control D 0.52→0.59 as the seats become contestable.

## 🟠 High — data quality

- [x] **Down-weight partisan polls.** (ce3bbf3)
      Done. Added `PollData.IsPartisan` and halved the weight of flagged polls in `GetWeight`
      (reused the property in the polls DTO). Verified FL-SEN's average now matches the 0.5x-partisan
      computation exactly (margin -4.98 vs the old -4.72).

- [x] **Fix or remove the dead approval input.** (b8d14da) — Removed.
      Deleted `IApprovalSource`/`ApprovalAggregator`/`ApprovalDataPoint` and dropped the input from
      the orchestrator, `ForecastInputs`, and the client types/copy. The national-environment
      fallback is now a named `DefaultMidtermEnvironment` constant, not a 50%-approval projection.
      (Unused `ApprovalRating` table + ForecastHistory approval columns left to avoid a migration.)

- [x] **Populate pollster ratings.** (f274d1e) — Wired.
      Added `PollsterRatings` (approximate reputational tiers for ~30 common pollsters, substring
      match) and populate `PollsterRating` at parse time, so gold-standard polls outweigh
      partisan/low-quality ones. Verified AK-SEN's average matches the rating-weighted computation
      exactly (NYT/Siena at 1.3x vs the old uniform 0.9x).

- [x] **Use the model's SE in the polling input card.** (b5248e9)
      Done. `PollingAverage.GetDemWinProbability` now takes the SE and delegates to
      `ForecastMath.MarginToProbability` (dropped a duplicated `NormalCdf`); `BuildForecast` passes
      the race's `MarginStdDev`, and the RacePage fallback uses SE 6. Verified the polling win prob
      now equals `cdf(margin/SE)` per race and is far less overconfident than the old SE 3.5.

## 🟡 Medium — methodology upgrades (closing the gap to pro models)

- [x] **Fat-tailed simulation errors.** (a9bd142)
      Done. `MonteCarloSimulator` now draws the national swing and each race's idiosyncratic error
      from a Student-t (5 dof) rescaled to the same SD via `SampleTError`. Verified: SD preserved
      (6.0→5.99), excess kurtosis ~5.4 (t(5) theory 6), 4.3x the >3σ tail mass; near-tied Senate
      control eases toward 50%.

- [x] **Regional/correlated error structure.** One national swing is good but coarse; add a small
      regional (or PVI-similarity) error component shared by clusters of similar states/districts
      so a polling miss in OH also hits IA/WI more than NV. Biggest effect on chamber odds.
      DONE (f647f0a): per-Census-region swing (RegionalErrorStdDev=2.0) drawn once per sim, shared
      by every race in the region; variance split national+regional+idiosyncratic so expected seats
      unchanged, seat-total tails widen. StateToRegion map + GetRegion in MonteCarloSimulator.

- [x] **Two-party normalization / undecided allocation.** (e94b2a3)
      Done. Added `PollingAverage.TwoPartyMargin` (undecideds split proportionally) and use it in
      `BlendMargins` and the polling win probability, so polls are on the same final-result scale as
      market/fundamentals; the raw `Margin` stays for display. Verified pollingWinProbability now
      equals `cdf(twoPartyMargin/SE)` per race.

- [x] **Add pollster house effects.** Estimate per-pollster partisan lean from deviations vs. the
      race average and subtract it, rather than only weighting by quality.
      DONE (cb348eb): PollsterHouseEffects.Estimate — leave-one-out consensus per race, per-pollster
      mean deviation shrunk toward 0 (K=4, min 3 polls, clamp ±6), public polls only. Calculator
      de-biases each poll's margin before averaging; WikipediaPollingClient estimates across all
      persisted polls and caches per refresh. Verified 8 races match the de-biased average exactly
      (OH-SEN flips -0.76 -> +0.12 on pollster composition).

## 🟢 Low — code hygiene / robustness

- [x] **Deduplicate the shared data and math.** `StatePVI` exists in both `RaceService` and
      `CookPVIProvider`; district PVI exists in two places with *opposite sign conventions*;
      `NormalCdf` is copy-pasted in three files (`ForecastMath`, `PollingAverage`, `RaceService`).
      Single source of truth for each.
      DONE (5324731): new `CookPvi` (Dem-positive lean) holds the one state table; `GetDistrictLean`
      flips the R-positive district table once and falls back to state lean. RaceService +
      CookPVIProvider both route through it; RaceService NormalCdf -> ForecastMath. (PollingAverage
      was already migrated in earlier work.) Behavior verified unchanged.

- [x] **Thread-safe RNG in `MonteCarloSimulator`.** It's a singleton sharing one `Random` across
      concurrent requests; use `Random.Shared`. DONE (c72ea02): dropped the instance Random, now
      uses Random.Shared in SampleStandardNormal (made static).

- [x] **Split the refresh cadences in `DataRefreshService`.** Both the 15-min market check and the
      6-hour polling check call the same `RefreshAllDataAsync`, so poll caches are cleared every
      15 minutes (impolite to Wikipedia). Refresh markets and polls on their own schedules.
      DONE (383d808): orchestrator now has RefreshMarketDataAsync (short) + RefreshPollingDataAsync
      (long), sharing ClearForecastCacheAsync; service calls each on its own cadence. Manual
      /forecast/refresh endpoint still refreshes all.

- [ ] **Market weight: revisit empirically.** → moved to FIX LIST item 10.

## 🆕 UI features (requested 2026-07-09, ordered easiest → hardest)

- [ ] **Landing pages for House districts.** → moved to FIX LIST item 8.

- [x] **Support viable independents (e.g. Dan Osborn / NE-SEN).** DONE (b2a8313 backend, e2b53e0 UI):
      `IndependentChallengers` static table designates a viable independent to occupy the challenger
      slot; they flow through the two-way engine (RaceService/RacesController assign the D-side by slot
      not party), with their own prior (Osborn R+6, not the seat's generic R+25), the market mapped to
      "Will Republicans win" (1−P(R) = the independent), and polling dropped (D-vs-R polls price the
      token Democrat). NE-SEN now = Osborn ~34% vs Ricketts (Lean R). UI: cards/race page/map pick the
      challenger as the non-Republican candidate and color independents gold/yellow (map fill uses a
      gold ramp when the independent leads; tooltip shows the independent by name).
      Race-page/state-map/chart challenger coloring made gold too (fb80f74) — the trend chart, mobile
      headline, and StateMap candidate list now follow the challenger's party color.
      FOLLOW-UPS: (a) House independents aren't wired into USDistrictMap (none designated yet); (b) an
      independent win counts as a Dem seat in the chamber sim (documented simplification — Osborn would
      caucus with neither); (c) adding more independents = add to IndependentChallengers + remap their
      market + set their prior.


- [x] **Show the projected result on the House district hover tooltip.** When hovering a district,
      display the expected margin (e.g. `D+2` / `R+5`) in the top-right corner of the card.
      DONE (30d05ba): top-right label in USDistrictMap's map-tooltip header, from the blended forecast's
      expectedDemMargin (blue D+ / red R+), via a formatMargin helper.

- [x] **Remove the Forecast / Polymarket / Polls data-source selector.** Users should only see the
      combined Forecast (the map). Delete the source-toggle buttons from `ChamberForecast.tsx` (both
      the compact sidebar and full-mode render blocks) and the equivalent `DataSource` toggle on
      `RacePage.tsx`; hardcode `dataSource = 'combined'` and drop the now-dead markets/polling
      branches, props, and fallback notes. KEEP the actual polls list on the race/state landing pages
      — that's `RacePage`'s `pollsData` (`/polls`) section, which is independent of the toggle
      (verified), so it stays visible. Touches two components + downstream cleanup — slightly harder.
      DONE (7295c53): ChamberForecast rewritten to combined-only (dead full mode removed); HomePage +
      RacePage toggles gone; maps get a fixed 'combined'. Polls now render on mobile RacePage too (were
      only in the old 'polling' lens). −556 lines; typecheck + prod build clean.

---

## ✅ MOSTLY DONE — Real House district candidates

Scraped 2026 House nominees + incumbents from the per-state Wikipedia district infoboxes
into `ElectionDataProvider.HouseNominees.cs` (360/435 districts resolved on 2026-07-07);
`CreateHouseRace` now uses real names + per-candidate incumbency, and RaceService no longer
overrides incumbency for resolved districts. Remaining: refresh as more primaries conclude
(MI Aug 4, AZ Jul 21, etc.) to fill the ~75 unresolved districts and replace presumptive
nominees. Re-run the throwaway House-discovery scraper to regenerate.


## ✅ DONE — Prediction-over-time (phases 1 & 2)

Phase 1: per-race model history (daily logging + retrospective backfill from June 1),
`BackfillModelHistoryAsync`, `/api/forecast/backfill`, startup hook, RacePage chart.
Phase 2: Senate control-over-time — `BackfillChamberHistoryAsync` runs the Monte Carlo over
each day's stored per-race history, `GET /api/forecast/chamber/{type}/history`, and a
`ControlSparkline` in the ChamberForecast sidebar (Senate). House chamber line not backfilled
(no per-district history).

## ✅ DONE — Auto-generate Polymarket Senate + Governor market ID mapping

Built `ElectionForecaster.MarketDiscovery` (Gamma API → race_id → Democrat-market id).
Run: `dotnet run --project ElectionForecaster.MarketDiscovery`. Resolved 66/69; the 3
misses (AK-SEN, AK-GOV, CA-GOV) are non-two-party and kept hand-picked. Regenerated the
`RaceIdToMarketId` map in PolymarketClient — fixed off-by-one Republican-market IDs, added
missing MT-SEN, and dropped spurious MO-SEN. Original plan below for reference.

## Auto-generate Polymarket Senate + Governor market ID mapping (build-time generator)

**Goal:** Stop hand-collecting Polymarket market IDs from the network tab. Build a
build-time generator that discovers the `race_id -> market_id` mapping via the Gamma
API, then paste the verified result into the app.

**Scope:** Senate + Governor only. House is intentionally excluded — Polymarket has no
general-election markets for individual House districts (a search for "House district
2026" returns only a stray NY-08 primary), so there is nothing to map. House stays on
fundamentals + polling.

### Approach (option A — static, reviewable)
Write a small discovery routine (throwaway console app or a `dotnet` script), run it
once, and drop the verified mapping back into
`ElectionForecaster.Infrastructure/DataSources/PredictionMarkets/PolymarketClient.cs`
(the `RaceIdToMarketId` dictionary), or externalize it to a JSON file the client loads.
Runtime behavior is unchanged; the map stays static and diff-able.

### Discovery algorithm (verified against existing data)
For each state's Senate/Governor race:
1. Resolve the general-election event by slug pattern:
   - Governor: `{state-slug}-governor-winner-2026`
   - Senate:   `{state-slug}-senate-winner-2026`
   - (Use `GET https://gamma-api.polymarket.com/public-search?q=...` to find the event,
     then filter to the `-winner-2026` event — exclude `*-primary-*` and `*-runoff-*`.)
2. Fetch the event: `GET https://gamma-api.polymarket.com/events/{eventId}`.
3. From `event.markets[]`, take the market where `groupItemTitle == "Democrat"`.
   Its numeric `id` is the value stored in `RaceIdToMarketId` (the existing parser
   derives Rep odds as 1 - Dem, or reads the `"Republican"` market).

**Verified example:** event `georgia-governor-winner-2026` (id 57161) -> market
`629337` (groupItemTitle "Democrat") == the hand-collected ID already in the code.

### Notes / gotchas
- Multi-word state slugs are hyphenated: `north-carolina-senate-winner-2026`.
- Skip primary/runoff events (`*-primary-winner`, `*-runoff-*`).
- Some races are not strictly Dem/Rep (code already comments these:
  NE-SEN, MI-GOV, CA-GOV, AK-GOV) — handle/flag the non-two-party ones.
- Consider emitting a warning for any configured race that the generator can't resolve,
  and for any newly discovered `-winner-2026` event not yet in the map.
- Follow-up (option B, later): make discovery run at startup/refresh so the map is
  self-updating as new race markets open, with the static map as a fallback.

## Race detail page (RacePage) UI cleanup

Elements below live on the race detail page (`election-forecaster-client/src/pages/RacePage.tsx`);
incumbency also surfaces via `RaceCard` on the state page.

- [x] **Fix incumbency flags on candidates.** Michigan's is wrong — verify `IsIncumbent`
      against reality (MI Senate 2026 is an open seat with Peters retiring, so neither
      candidate should show "(Incumbent)"). Source is the candidate data in
      `ElectionForecaster.Infrastructure/Data/ElectionDataProvider.cs`. Audit other states too.
- [x] **Remove the "Democratic Nominee" / "Republican Nominee" name line** shown under the
      big win-probability percentages (the `demCandidate?.name` / `repCandidate?.name` under
      the Win Probability numbers).
- [x] **Remove the "Combined forecast (markets + polling + fundamentals)" subtitle** under
      the Win Probability header (the `dataSource === 'combined'` caption).
- [x] **Remove the "Confidence: 95% | Last Updated: ..." footer** in the Forecast Inputs
      section.
- [x] **Remove the "Updated: <date>" sublabel** on the Prediction Markets input card
      (`marketLastUpdated`).
- [x] **Remove the poll-count sublabel** ("N polls") under the Polling Average input card.
- [x] **Remove the "Prediction Over Time" component entirely when there's no history**
      (currently it renders a "No historical data available" placeholder — drop the whole
      section instead of showing the empty state).

## Main page (home dashboard) UI cleanup

- [x] **Remove the "50 needed for majority" label under Senate Projected Seats.**
      It's the `{majorityNeeded} needed for majority` label in
      `election-forecaster-client/src/components/forecast/ChamberForecast.tsx` (~line 371).
      (Precedent: the majority line/label was already hidden for governors in commit
      4e3fa9f — do the same for Senate.)
