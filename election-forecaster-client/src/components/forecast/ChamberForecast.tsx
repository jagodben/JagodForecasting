import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Race, RaceType, RaceRating } from '../../types';
import { forecastApi } from '../../services/api';
import { ProbabilityTrendChart } from '../charts/ProbabilityTrendChart';
import { timeAgo } from '../../utils/time';

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
}

interface SeatProjection {
  democrat: number;
  republican: number;
  independent: number;
  tossup: number;
}

export const ChamberForecast = ({ races, raceType }: ChamberForecastProps) => {
  // Fetch detailed (blended) forecasts for all races of this type in a single batched request
  // (shares its cache with the map via the same query key).
  const { data: detailedForecasts, isLoading: isLoadingForecasts } = useQuery({
    queryKey: ['forecasts', raceType],
    queryFn: () => forecastApi.getAll(raceType),
    enabled: races.length > 0,
  });

  // Chamber control-over-time (Senate has a backfilled model history series).
  const { data: chamberHistory } = useQuery({
    queryKey: ['chamberHistory', raceType],
    queryFn: () => forecastApi.getChamberHistory('Senate'),
    enabled: raceType === RaceType.Senate,
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

  // The model's final Senate prediction is the Monte Carlo (what the chart plots): its control
  // probability AND its expected seat count come from the same 10k-simulation run. Use BOTH so the
  // "Win Probability" and "Projected Seats" agree — otherwise the seat number is a cruder favored-race
  // tally that counts every sub-50% near-tie (OH, IA, TX) wholly for the other side and can disagree
  // with the control probability (e.g. R "ahead" 51-49 on the tally while D is favored 58% to control).
  // Require a populated expectedDemSeats: if the history point is missing seat data, fall back to the
  // favored-race tally for BOTH the control number and the seats, so they stay consistent (rather than
  // showing a valid control probability next to 0 seats).
  const lastSenatePoint = raceType === RaceType.Senate && chamberHistory && chamberHistory.length > 0
    ? chamberHistory[chamberHistory.length - 1]
    : null;
  const senateModel = lastSenatePoint && lastSenatePoint.expectedDemSeats > 0 ? lastSenatePoint : null;
  const modelSenateControl = senateModel ? Math.round(senateModel.demControlProbability * 1000) / 10 : null;
  const demVictoryOdds = modelSenateControl != null ? modelSenateControl : rawDemVictoryOdds;
  const repVictoryOdds = Math.round((100 - demVictoryOdds) * 10) / 10;

  // Most-recent forecast generation time across this chamber's races → a data-freshness label.
  const lastUpdatedLabel = (() => {
    if (!detailedForecasts || detailedForecasts.length === 0) return null;
    const latest = detailedForecasts.reduce((max, f) =>
      f.lastUpdated > max ? f.lastUpdated : max, detailedForecasts[0].lastUpdated);
    return timeAgo(latest);
  })();

  const chamberName = raceType === RaceType.Senate ? 'Senate' : raceType === RaceType.House ? 'House' : 'Governors';
  // Governors have no chamber majority, so the "seat" total is just the races up in 2026.
  const totalSeats = raceType === RaceType.Senate ? 100 : raceType === RaceType.House ? 435 : races.length;
  const majorityNeeded = raceType === RaceType.Senate ? 50 : raceType === RaceType.House ? 218 : 26;
  const seatLabel = raceType === RaceType.Governor ? 'Projected Governorships' : 'Projected Seats';

  // Seats NOT up for election in 2026, by the party currently holding them. These must match the
  // Monte Carlo baselines that drive the control probability, or the projected-seat total won't sum
  // to 100. The app models 35 Senate races (33 Class-2 seats + the FL and OH specials, both held by
  // appointed Republicans), so the not-up pool is 34 D / 31 R = 65; 65 + 35 races = 100. (The two
  // Republican specials moved from the not-up baseline into the modeled races, which is why this is
  // 31, not 33.) All 435 House seats are up (no holdovers). Governors have no chamber majority, so
  // only the 2026 races up are shown — no fabricated not-up holdovers.
  const NOT_UP_HELD: Record<'Senate' | 'House' | 'Governors', { dem: number; rep: number }> = {
    Senate: { dem: 34, rep: 31 },
    House: { dem: 0, rep: 0 },
    Governors: { dem: 0, rep: 0 },
  };
  const assumedDemHeld = NOT_UP_HELD[chamberName].dem;
  const assumedRepHeld = NOT_UP_HELD[chamberName].rep;

  // Senate: take the seat totals from the Monte Carlo's expected seats (rounded), so they're
  // consistent with the control probability above. Other chambers use the favored-race tally.
  const totalDemSeats = senateModel ? Math.round(senateModel.expectedDemSeats) : seatProjection.democrat + assumedDemHeld;
  const totalRepSeats = senateModel ? totalSeats - totalDemSeats : seatProjection.republican + assumedRepHeld;

  // Seat bar. For the Senate (expected-seats mode) a clean two-color split at the projected totals,
  // so the bar, the number, and the win probability all agree. Otherwise the Solid D → Solid R rating
  // composition (with assumed-held folded into the Solid ends).
  const seatSegments = senateModel
    ? [
        { rating: RaceRating.SolidDem, count: totalDemSeats, color: '#123f8f' },
        { rating: RaceRating.SolidRep, count: totalRepSeats, color: '#9c150b' },
      ].filter(s => s.count > 0)
    : RATING_ORDER.map(rating => {
        let count = seatsByRating.get(rating) || 0;
        if (rating === RaceRating.SolidDem) count += assumedDemHeld;
        if (rating === RaceRating.SolidRep) count += assumedRepHeld;
        return { rating, count, color: RATING_COLORS[rating] };
      }).filter(s => s.count > 0);

  return (
    <div className="forecast-sidebar">
      <h3 className="forecast-sidebar__title">{chamberName} Forecast</h3>

      {/* Win Probability - hide for governors (no chamber majority) */}
      {raceType !== RaceType.Governor && (
        <div className="forecast-sidebar__section">
          <div className="forecast-sidebar__label">Win Probability</div>
          <div className="forecast-sidebar__seats">
            <span style={{ color: '#123f8f', fontWeight: 'bold', fontSize: '18px' }}>{demVictoryOdds}%</span>
            <span style={{ color: '#9c150b', fontWeight: 'bold', fontSize: '18px' }}>{repVictoryOdds}%</span>
          </div>
          <div className="forecast-sidebar__seat-bar">
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
          <span style={{ color: '#123f8f', fontWeight: 'bold', fontSize: '18px' }}>{totalDemSeats}</span>
          <span style={{ color: '#9c150b', fontWeight: 'bold', fontSize: '18px' }}>{totalRepSeats}</span>
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
      </div>

      {/* Dem control probability over time (Senate) */}
      {chamberHistory && chamberHistory.length >= 2 && (
        <div className="forecast-sidebar__section">
          <div className="forecast-sidebar__label">Race Timeline</div>
          <ProbabilityTrendChart
            data={chamberHistory.map(d => ({ date: d.date, demValue: d.demControlProbability }))}
            demLabel="Dem"
            repLabel="Rep"
          />
        </div>
      )}

      {isLoadingForecasts && (
        <div className="forecast-sidebar__loading">Loading forecast data...</div>
      )}

      {lastUpdatedLabel && (
        <div style={{ marginTop: '12px', fontSize: '11px', color: '#888888', textAlign: 'center' }}>
          Updated {lastUpdatedLabel}
        </div>
      )}
    </div>
  );
};
