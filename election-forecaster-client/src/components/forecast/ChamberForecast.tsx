import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Race, RaceType, RaceRating } from '../../types';
import { forecastApi } from '../../services/api';
import { ProbabilityTrendChart } from '../charts/ProbabilityTrendChart';

// Rating order from left (Solid D) to right (Solid R)
const RATING_ORDER: RaceRating[] = [
  RaceRating.SolidDem,
  RaceRating.LikelyDem,
  RaceRating.LeanDem,
  RaceRating.TiltDem,
  RaceRating.TiltRep,
  RaceRating.LeanRep,
  RaceRating.LikelyRep,
  RaceRating.SolidRep,
];

const RATING_COLORS: Record<RaceRating, string> = {
  [RaceRating.SolidDem]: '#123f8f',
  [RaceRating.LikelyDem]: '#2e63bd',
  [RaceRating.LeanDem]: '#5a8fd6',
  [RaceRating.TiltDem]: '#9dbff0',
  [RaceRating.TiltRep]: '#f4aa9b',
  [RaceRating.LeanRep]: '#e2694f',
  [RaceRating.LikelyRep]: '#cf2f1a',
  [RaceRating.SolidRep]: '#9c150b',
};

// Convert probability to rating
const probabilityToRating = (demProb: number): RaceRating => {
  if (demProb >= 0.90) return RaceRating.SolidDem;
  if (demProb >= 0.70) return RaceRating.LikelyDem;
  if (demProb >= 0.55) return RaceRating.LeanDem;
  if (demProb > 0.50) return RaceRating.TiltDem;
  if (demProb >= 0.45) return RaceRating.TiltRep;
  if (demProb >= 0.30) return RaceRating.LeanRep;
  if (demProb >= 0.10) return RaceRating.LikelyRep;
  return RaceRating.SolidRep;
};

interface ChamberForecastProps {
  races: Race[];
  raceType: RaceType.Senate | RaceType.House | RaceType.Governor;
  // Compact renders a slim summary card (win-odds bar + projected seats) for the mobile
  // map pane. It reuses the exact same computations and query cache as the full sidebar,
  // so the two always show the same numbers.
  compact?: boolean;
}

interface SeatProjection {
  democrat: number;
  republican: number;
  independent: number;
  tossup: number;
}

export const ChamberForecast = ({ races, raceType, compact = false }: ChamberForecastProps) => {
  // Fetch detailed (blended) forecasts for all races of this type in a single batched request
  // (shares its cache with the map via the same query key).
  const { data: detailedForecasts, isLoading: isLoadingForecasts } = useQuery({
    queryKey: ['forecasts', raceType],
    queryFn: () => forecastApi.getAll(raceType),
    enabled: races.length > 0,
  });

  // Chamber control-over-time (Senate and House both have backfilled model history series;
  // governors have no chamber majority, so no timeline).
  const { data: chamberHistory, isLoading: isLoadingHistory } = useQuery({
    queryKey: ['chamberHistory', raceType],
    queryFn: () => forecastApi.getChamberHistory(raceType),
    enabled: raceType !== RaceType.Governor,
  });

  // Helper: overall chamber Dem win probability from the seat projection.
  const calculateCombinedOdds = (projection: SeatProjection, raceCount: number, rt: RaceType): number => {
    if (rt === RaceType.Senate) {
      const demTotal = projection.democrat + 0.5 * projection.tossup;
      const seatsNeeded = raceCount / 2;
      const advantage = (demTotal - seatsNeeded) / raceCount;
      return Math.round((50 + advantage * 100) * 10) / 10;
    } else if (rt === RaceType.House) {
      const demTotal = projection.democrat + 0.5 * projection.tossup;
      const advantage = (demTotal - 218) / 50;
      return Math.round((50 + advantage * 30) * 10) / 10;
    } else {
      // Governor: simple ratio of D wins among races up
      const demTotal = projection.democrat + 0.5 * projection.tossup;
      const advantage = (demTotal - raceCount / 2) / raceCount;
      return Math.round((50 + advantage * 100) * 10) / 10;
    }
  };

  const { seatProjection, seatsByRating, demVictoryOdds: rawDemVictoryOdds } = useMemo(() => {
    const projection: SeatProjection = { democrat: 0, republican: 0, independent: 0, tossup: 0 };

    const ratingCounts = new Map<RaceRating, number>();
    RATING_ORDER.forEach(r => ratingCounts.set(r, 0));

    races.forEach(race => {
      const detailed = detailedForecasts?.find(f => f.raceId === race.id);

      // Combined (blended) win probability — the same value the map shows.
      const demProb = detailed
        ? detailed.demWinProbability
        : (race.forecasts.find(f =>
            race.candidates.find(c => c.id === f.candidateId)?.party === 'Democrat'
          )?.winProbability ?? 0.5);

      // Categorize by rating (must match the respective map coloring).
      const barRating = raceType === RaceType.House ? race.rating : probabilityToRating(demProb);
      ratingCounts.set(barRating, (ratingCounts.get(barRating) || 0) + 1);

      if (demProb >= 0.5) projection.democrat++;
      else projection.republican++;
    });

    const demOdds = Math.max(5, Math.min(95, calculateCombinedOdds(projection, races.length, raceType)));

    return { seatProjection: projection, seatsByRating: ratingCounts, demVictoryOdds: demOdds };
  }, [races, raceType, detailedForecasts]);

  // Senate/House headline numbers come from the Monte Carlo (the same run the chart plots), so
  // "Win Probability" and "Projected Seats" always agree. If the latest history point lacks seat
  // data, fall back to the favored-race tally for BOTH numbers. Governors have no chamber sim.
  const lastSimPoint = raceType !== RaceType.Governor && chamberHistory && chamberHistory.length > 0
    ? chamberHistory[chamberHistory.length - 1]
    : null;
  const simModel = lastSimPoint && lastSimPoint.expectedDemSeats > 0 ? lastSimPoint : null;
  const modelControl = simModel ? Math.round(simModel.demControlProbability * 1000) / 10 : null;
  const demVictoryOdds = modelControl != null ? modelControl : rawDemVictoryOdds;
  const repVictoryOdds = Math.round((100 - demVictoryOdds) * 10) / 10;

  const chamberName = raceType === RaceType.Senate ? 'Senate' : raceType === RaceType.House ? 'House' : 'Governors';
  // Governors have no chamber majority, so the "seat" total is just the races up in 2026.
  const totalSeats = raceType === RaceType.Senate ? 100 : raceType === RaceType.House ? 435 : races.length;
  const majorityNeeded = raceType === RaceType.Senate ? 50 : raceType === RaceType.House ? 218 : 26;
  const seatLabel = raceType === RaceType.Governor ? 'Projected Governorships' : 'Projected Seats';

  // Senate seats NOT up in 2026, by current party (34 D / 31 R; must match the Monte Carlo
  // baselines or the total won't sum to 100). All 435 House seats are up; governors have no
  // chamber majority.
  const NOT_UP_HELD: Record<'Senate' | 'House' | 'Governors', { dem: number; rep: number }> = {
    Senate: { dem: 34, rep: 31 },
    House: { dem: 0, rep: 0 },
    Governors: { dem: 0, rep: 0 },
  };
  const assumedDemHeld = NOT_UP_HELD[chamberName].dem;
  const assumedRepHeld = NOT_UP_HELD[chamberName].rep;

  // Sim path: expected seats to one decimal (an average across simulations, so it legitimately
  // differs from counting the map's colors). Tally path matches the map.
  const totalDemSeats = simModel ? Math.round(simModel.expectedDemSeats * 10) / 10 : seatProjection.democrat + assumedDemHeld;
  const totalRepSeats = simModel ? Math.round((totalSeats - totalDemSeats) * 10) / 10 : seatProjection.republican + assumedRepHeld;
  const formatSeats = (n: number) => simModel ? n.toFixed(1) : String(n);

  // Seat bar: the familiar Solid D → Solid R rating gradient (assumed-held folded into the Solid ends).
  const ratingSegments = RATING_ORDER.map(rating => {
    let count = seatsByRating.get(rating) || 0;
    if (rating === RaceRating.SolidDem) count += assumedDemHeld;
    if (rating === RaceRating.SolidRep) count += assumedRepHeld;
    return { rating, count, color: RATING_COLORS[rating] };
  });

  // Rescale the gradient's two sides so the blue/red boundary lands at the sim's expected seats
  // (keeps the bar consistent with the headline numbers).
  const DEM_RATINGS = new Set([RaceRating.SolidDem, RaceRating.LikelyDem, RaceRating.LeanDem, RaceRating.TiltDem]);
  const seatSegments = (() => {
    if (!simModel) return ratingSegments.filter(s => s.count > 0);
    const demRaw = ratingSegments.filter(s => DEM_RATINGS.has(s.rating)).reduce((a, s) => a + s.count, 0);
    const repRaw = ratingSegments.filter(s => !DEM_RATINGS.has(s.rating)).reduce((a, s) => a + s.count, 0);
    return ratingSegments
      .map(s => {
        const scale = DEM_RATINGS.has(s.rating)
          ? (demRaw > 0 ? totalDemSeats / demRaw : 0)
          : (repRaw > 0 ? totalRepSeats / repRaw : 0);
        return { ...s, count: s.count * scale };
      })
      .filter(s => s.count > 0);
  })();

  // Show the numbers only once their data has loaded. The sim-based headline needs both the
  // per-race forecasts and the chamber history; until those arrive, computing from the fallback
  // tally would flash a different (cruder) number that then snaps to the correct one.
  const notReady = isLoadingForecasts || (raceType !== RaceType.Governor && isLoadingHistory);

  // Mobile map-pane summary: chamber odds + projected seats at a glance, plus a hint that the
  // map is tappable. Rendered nothing while loading (same no-flash rule as the sidebar).
  if (compact) {
    if (notReady) return null;
    return (
      <div className="mobile-chamber-card">
        <div className="mobile-chamber-card__title">{chamberName} Forecast</div>
        {raceType !== RaceType.Governor && (
          <>
            <div className="mobile-chamber-card__probs">
              <span style={{ color: '#123f8f' }}>{demVictoryOdds}%</span>
              <div className="mobile-chamber-card__bar" role="img"
                   aria-label={`Win probability: Democrats ${demVictoryOdds}%, Republicans ${repVictoryOdds}%`}>
                <div style={{ width: `${demVictoryOdds}%`, backgroundColor: '#123f8f' }} />
                <div style={{ width: `${repVictoryOdds}%`, backgroundColor: '#9c150b' }} />
              </div>
              <span style={{ color: '#9c150b', textAlign: 'right' }}>{repVictoryOdds}%</span>
            </div>
            <div className="mobile-chamber-card__caption">
              Chance of controlling the {chamberName}
            </div>
          </>
        )}
        <div className="mobile-chamber-card__seats">
          <span>{seatLabel}</span>
          <span>
            <b style={{ color: '#123f8f' }}>{formatSeats(totalDemSeats)}</b>
            <span className="mobile-chamber-card__dash">–</span>
            <b style={{ color: '#9c150b' }}>{formatSeats(totalRepSeats)}</b>
          </span>
        </div>
        <div className="mobile-chamber-card__hint">
          Tap a {raceType === RaceType.House ? 'district' : 'state'} on the map for details
        </div>
      </div>
    );
  }

  return (
    <div className="forecast-sidebar">
      <h3 className="forecast-sidebar__title">{chamberName} Forecast</h3>

      {notReady ? (
        <div className="forecast-sidebar__loading">Loading forecast…</div>
      ) : (
      <>
      {/* Win Probability - hide for governors (no chamber majority) */}
      {raceType !== RaceType.Governor && (
        <div className="forecast-sidebar__section">
          <div className="forecast-sidebar__label">Win Probability</div>
          <div className="forecast-sidebar__seats">
            <span style={{ color: '#123f8f', fontWeight: 'bold', fontSize: '18px' }}>{demVictoryOdds}%</span>
            <span style={{ color: '#9c150b', fontWeight: 'bold', fontSize: '18px' }}>{repVictoryOdds}%</span>
          </div>
          <div className="forecast-sidebar__seat-bar" role="img"
               aria-label={`Win probability: Democrats ${demVictoryOdds}%, Republicans ${repVictoryOdds}%`}>
            <div style={{ width: `${demVictoryOdds}%`, backgroundColor: '#123f8f' }} />
            <div style={{ width: `${repVictoryOdds}%`, backgroundColor: '#9c150b' }} />
          </div>
          <div className="forecast-sidebar__party-labels">
            <span>Democrats</span>
            <span>Republicans</span>
          </div>
        </div>
      )}

      {/* Projected Seats */}
      <div className="forecast-sidebar__section">
        <div className="forecast-sidebar__label">{seatLabel}</div>
        <div className="forecast-sidebar__seats">
          <span style={{ color: '#123f8f', fontWeight: 'bold', fontSize: '18px' }}>{formatSeats(totalDemSeats)}</span>
          <span style={{ color: '#9c150b', fontWeight: 'bold', fontSize: '18px' }}>{formatSeats(totalRepSeats)}</span>
        </div>
        <div className="forecast-sidebar__seat-bar">
          {seatSegments.map(seg => (
            <div key={seg.rating} style={{
              width: `${(seg.count / totalSeats) * 100}%`,
              backgroundColor: seg.color,
            }} />
          ))}
          {raceType !== RaceType.Governor && (
            <div className="forecast-sidebar__majority-line" style={{
              left: `${(majorityNeeded / totalSeats) * 100}%`,
            }} />
          )}
        </div>
        {simModel && (
          <div style={{ marginTop: '6px', fontSize: '11px', color: '#6b6b6b', lineHeight: 1.4 }}>
            Average of 10,000 simulations. Close races count fractionally toward both
            parties, so this can differ from tallying each race&rsquo;s current leader on the map.
          </div>
        )}
      </div>

      {/* Dem control probability over time (Senate) */}
      {chamberHistory && chamberHistory.length >= 2 && (
        <div className="forecast-sidebar__section">
          <div className="forecast-sidebar__label">Race Timeline</div>
          <ProbabilityTrendChart
            data={chamberHistory.map(d => ({ date: d.date, demValue: d.demControlProbability }))}
            demLabel="Dem"
            repLabel="Rep"
            pillScale={2}
          />
        </div>
      )}

      <div style={{ marginTop: '12px', fontSize: '11px', color: '#6b6b6b', textAlign: 'center' }}>
        Updated daily at 8:00 AM ET
      </div>
      </>
      )}
    </div>
  );
};
