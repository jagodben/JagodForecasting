import { useMemo, useState, useEffect } from 'react';
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
  [RaceRating.SolidDem]: '#0033AA',
  [RaceRating.LikelyDem]: '#2266DD',
  [RaceRating.LeanDem]: '#5599EE',
  [RaceRating.TiltDem]: '#99CCFF',
  [RaceRating.TiltRep]: '#FFCC99',
  [RaceRating.LeanRep]: '#E07070',
  [RaceRating.LikelyRep]: '#DD4422',
  [RaceRating.SolidRep]: '#AA0000',
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

type DataSource = 'combined' | 'markets' | 'polling';

interface ChamberForecastProps {
  races: Race[];
  raceType: RaceType.Senate | RaceType.House | RaceType.Governor;
  compact?: boolean;
  dataSource?: DataSource;
  onDataSourceChange?: (source: DataSource) => void;
  onDataAvailabilityChange?: (hasMarket: boolean, hasPolling: boolean) => void;
}

interface SeatProjection {
  democrat: number;
  republican: number;
  independent: number;
  tossup: number;
}

export const ChamberForecast = ({ races, raceType, compact = false, dataSource: externalDataSource, onDataSourceChange, onDataAvailabilityChange }: ChamberForecastProps) => {
  const [internalDataSource, setInternalDataSource] = useState<DataSource>('combined');
  const dataSource = externalDataSource ?? internalDataSource;
  const setDataSource = onDataSourceChange ?? setInternalDataSource;

  // Fetch detailed forecasts for all races of this type in a single batched request
  // (shares its cache with the map via the same query key).
  const { data: detailedForecasts, isLoading: isLoadingForecasts } = useQuery({
    queryKey: ['forecasts', raceType],
    queryFn: () => forecastApi.getAll(raceType),
    enabled: races.length > 0,
  });

  // Fetch chamber-level market odds (overall control odds from Polymarket)
  const { data: chamberMarketOdds } = useQuery({
    queryKey: ['chamberMarketOdds', raceType],
    queryFn: () => forecastApi.getChamberMarketOdds(
      raceType === RaceType.Senate ? 'Senate' : raceType === RaceType.House ? 'House' : 'Governor'
    ),
    enabled: raceType === RaceType.Senate, // Only Senate has chamber market for now
  });

  // Chamber control-over-time (Senate has a backfilled model history series)
  const { data: chamberHistory } = useQuery({
    queryKey: ['chamberHistory', raceType],
    queryFn: () => forecastApi.getChamberHistory('Senate'),
    enabled: raceType === RaceType.Senate,
  });

  // Helper function to calculate combined odds from seat projection
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

  const { seatProjection, seatsByRating, demVictoryOdds: rawDemVictoryOdds, hasMarketData, hasPollingData, activeSource, marketCount, pollingCount } = useMemo(() => {
    // Calculate seat projections based on race forecasts
    const projection: SeatProjection = {
      democrat: 0,
      republican: 0,
      independent: 0,
      tossup: 0,
    };

    // Track seats by rating category
    const ratingCounts = new Map<RaceRating, number>();
    RATING_ORDER.forEach(r => ratingCounts.set(r, 0));

    // Track if we have market/polling data
    let marketsAvailable = 0;
    let pollingAvailable = 0;

    races.forEach(race => {
      // Find detailed forecast for this race
      const detailed = detailedForecasts?.find(f => f.raceId === race.id);

      // Check data availability
      if (detailed?.inputs.marketOdds != null) marketsAvailable++;
      if (detailed?.inputs.pollingAverage != null) pollingAvailable++;

      // Get the probability based on selected data source
      let demProb: number;

      if (dataSource === 'markets' && detailed?.inputs.marketOdds != null) {
        demProb = detailed.inputs.marketOdds;
      } else if (dataSource === 'polling' && detailed?.inputs.pollingWinProbability != null) {
        demProb = detailed.inputs.pollingWinProbability;
      } else if (detailed) {
        demProb = detailed.demWinProbability;
      } else {
        // Fallback to original forecast
        const demForecast = race.forecasts.find(f =>
          race.candidates.find(c => c.id === f.candidateId)?.party === 'Democrat'
        );
        demProb = demForecast?.winProbability || 0.5;
      }

      // Categorize by rating (must match respective map coloring)
      let barRating: RaceRating;
      if (raceType === RaceType.House) {
        // House map uses race.rating for combined, only overrides with source-specific data
        if (dataSource === 'markets' && detailed?.inputs.marketOdds != null) {
          barRating = probabilityToRating(detailed.inputs.marketOdds);
        } else if (dataSource === 'polling' && detailed?.inputs.pollingWinProbability != null) {
          barRating = probabilityToRating(detailed.inputs.pollingWinProbability);
        } else {
          barRating = race.rating;
        }
      } else {
        // Senate/Governor map always computes from demProb
        barRating = probabilityToRating(demProb);
      }
      ratingCounts.set(barRating, (ratingCounts.get(barRating) || 0) + 1);

      // Categorize the race based on which side has the lead
      if (demProb >= 0.5) {
        projection.democrat++;
      } else {
        projection.republican++;
      }
    });

    // Calculate overall victory odds based on selected data source
    let demOdds: number;
    let effectiveSource: DataSource = dataSource;

    if (dataSource === 'markets') {
      if (chamberMarketOdds) {
        demOdds = Math.round(chamberMarketOdds.demOdds * 1000) / 10;
      } else if (detailedForecasts) {
        const marketOdds = detailedForecasts
          .filter(f => f.inputs.marketOdds != null)
          .map(f => f.inputs.marketOdds!);
        if (marketOdds.length > 0) {
          demOdds = Math.round((marketOdds.reduce((a, b) => a + b, 0) / marketOdds.length) * 1000) / 10;
        } else {
          effectiveSource = 'combined';
          demOdds = calculateCombinedOdds(projection, races.length, raceType);
        }
      } else {
        effectiveSource = 'combined';
        demOdds = calculateCombinedOdds(projection, races.length, raceType);
      }
    } else if (dataSource === 'polling' && detailedForecasts) {
      // Derive chamber odds from the polling-based seat projection (built above from each
      // race's polling win probability), consistent with the combined path — not an average
      // of raw Dem vote-share percentages.
      if (pollingAvailable === 0) {
        effectiveSource = 'combined';
      }
      demOdds = calculateCombinedOdds(projection, races.length, raceType);
    } else {
      demOdds = calculateCombinedOdds(projection, races.length, raceType);
    }

    demOdds = Math.max(5, Math.min(95, demOdds));

    return {
      seatProjection: projection,
      seatsByRating: ratingCounts,
      demVictoryOdds: demOdds,
      hasMarketData: marketsAvailable > 0 || chamberMarketOdds != null,
      hasPollingData: pollingAvailable > 0,
      activeSource: effectiveSource,
      marketCount: marketsAvailable,
      pollingCount: pollingAvailable,
      chamberMarketOdds: chamberMarketOdds,
    };
  }, [races, raceType, dataSource, detailedForecasts, chamberMarketOdds]);

  // Notify parent of data availability changes
  useEffect(() => {
    onDataAvailabilityChange?.(hasMarketData, hasPollingData);
  }, [hasMarketData, hasPollingData, onDataAvailabilityChange]);

  // The model's final chamber prediction is the Monte Carlo control probability (what the chart
  // plots). Use it for the Senate headline in the combined/model view so the number and the chart
  // agree — the seat-share heuristic (rawDemVictoryOdds) ignored the not-up baseline and the 51-seat
  // threshold. Markets/polling modes keep their source-specific number.
  const modelSenateControl = raceType === RaceType.Senate && chamberHistory && chamberHistory.length > 0
    ? Math.round(chamberHistory[chamberHistory.length - 1].demControlProbability * 1000) / 10
    : null;
  const demVictoryOdds = (raceType === RaceType.Senate && activeSource === 'combined' && modelSenateControl != null)
    ? modelSenateControl
    : rawDemVictoryOdds;
  const repVictoryOdds = Math.round((100 - demVictoryOdds) * 10) / 10;

  // Most-recent forecast generation time across this chamber's races → a data-freshness label.
  const lastUpdatedLabel = (() => {
    if (!detailedForecasts || detailedForecasts.length === 0) return null;
    const latest = detailedForecasts.reduce((max, f) =>
      f.lastUpdated > max ? f.lastUpdated : max, detailedForecasts[0].lastUpdated);
    return timeAgo(latest);
  })();

  const chamberName = raceType === RaceType.Senate ? 'Senate' : raceType === RaceType.House ? 'House' : 'Governors';
  const totalSeats = raceType === RaceType.Senate ? 100 : raceType === RaceType.House ? 435 : 50;
  const majorityNeeded = raceType === RaceType.Senate ? 50 : raceType === RaceType.House ? 218 : 26;

  // Seats NOT up for election in 2026, by the party currently holding them. The Senate figures
  // match the Monte Carlo baseline that drives the control probability (post-2024 Senate, 33 Class-2
  // seats modeled as races → 34 D / 33 R not up), so the projected-seat total agrees with the
  // win-probability simulation instead of a flat 48% guess. All 435 House seats are up (no
  // holdovers). Governors have no chamber majority; the ~14 non-2026 governorships aren't modeled,
  // so they're split evenly as a neutral placeholder.
  const NOT_UP_HELD: Record<'Senate' | 'House' | 'Governors', { dem: number; rep: number }> = {
    Senate: { dem: 34, rep: 33 },
    House: { dem: 0, rep: 0 },
    Governors: { dem: 7, rep: 7 },
  };
  const assumedDemHeld = NOT_UP_HELD[chamberName].dem;
  const assumedRepHeld = NOT_UP_HELD[chamberName].rep;

  const totalDemSeats = seatProjection.democrat + assumedDemHeld;
  const totalRepSeats = seatProjection.republican + assumedRepHeld;

  // Build seat bar segments: Solid D → ... → Solid R, with assumed-held as Solid
  const seatSegments = RATING_ORDER.map(rating => {
    let count = seatsByRating.get(rating) || 0;
    if (rating === RaceRating.SolidDem) count += assumedDemHeld;
    if (rating === RaceRating.SolidRep) count += assumedRepHeld;
    return { rating, count, color: RATING_COLORS[rating] };
  }).filter(s => s.count > 0);

  const getSourceLabel = (source: DataSource) => {
    switch (source) {
      case 'combined': return 'Forecast';
      case 'markets': return 'Polymarket';
      case 'polling': return 'Polls';
    }
  };

  // ---------- COMPACT SIDEBAR MODE ----------
  if (compact) {
    return (
      <div className="forecast-sidebar">
        {/* Chamber title */}
        <h3 className="forecast-sidebar__title">
          {chamberName} Forecast
        </h3>

        {/* Win Probability - hide for governors */}
        {raceType !== RaceType.Governor && (
          <div className="forecast-sidebar__section">
            <div className="forecast-sidebar__label">Win Probability</div>
            <div className="forecast-sidebar__seats">
              <span style={{ color: '#0033AA', fontWeight: 'bold', fontSize: '18px' }}>{demVictoryOdds}%</span>
              <span style={{ color: '#AA0000', fontWeight: 'bold', fontSize: '18px' }}>{repVictoryOdds}%</span>
            </div>
            <div className="forecast-sidebar__seat-bar">
              <div style={{ width: `${demVictoryOdds}%`, backgroundColor: '#0033AA' }} />
              <div style={{ width: `${repVictoryOdds}%`, backgroundColor: '#AA0000' }} />
            </div>
            <div className="forecast-sidebar__party-labels">
              <span>Democrats</span>
              <span>Republicans</span>
            </div>
          </div>
        )}

        {/* Projected Seats */}
        <div className="forecast-sidebar__section">
          <div className="forecast-sidebar__label">Projected Seats</div>
          <div className="forecast-sidebar__seats">
            <span style={{ color: '#0033AA', fontWeight: 'bold', fontSize: '18px' }}>{totalDemSeats}</span>
            <span style={{ color: '#AA0000', fontWeight: 'bold', fontSize: '18px' }}>{totalRepSeats}</span>
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

        {/* Dem control probability over time (Senate) — Forecast view only */}
        {dataSource === 'combined' && chamberHistory && chamberHistory.length >= 2 && (
          <div className="forecast-sidebar__section">
            <div className="forecast-sidebar__label">Race Timeline</div>
            <ProbabilityTrendChart
              data={chamberHistory.map(d => ({ date: d.date, demValue: d.demControlProbability }))}
              demLabel="Dem"
              repLabel="Rep"
            />
          </div>
        )}

        {/* Data Source Toggle */}
        <div className="forecast-sidebar__section">
          <div className="forecast-sidebar__label">Data Source</div>
          <div className="forecast-sidebar__sources">
            {(['combined', 'markets', 'polling'] as DataSource[]).map((source) => {
              const isDisabled =
                (source === 'markets' && !hasMarketData) ||
                (source === 'polling' && !hasPollingData);

              return (
                <button
                  key={source}
                  onClick={() => !isDisabled && setDataSource(source)}
                  disabled={isDisabled}
                  className={`forecast-sidebar__source-btn ${dataSource === source ? 'forecast-sidebar__source-btn--active' : ''}`}
                  title={isDisabled ? `No ${source === 'markets' ? 'market' : 'polling'} data available` : ''}
                >
                  {getSourceLabel(source)}
                </button>
              );
            })}
          </div>
          {activeSource !== dataSource && dataSource !== 'combined' && (
            <div className="forecast-sidebar__fallback-note">
              Using combined (insufficient data)
            </div>
          )}
        </div>

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
  }

  // ---------- FULL MODE (for race detail pages, etc.) ----------
  return (
    <div style={{ marginTop: '24px', width: '100%', maxWidth: '1000px', margin: '24px auto 0' }}>
      {/* Loading indicator */}
      {isLoadingForecasts && (
        <div style={{
          textAlign: 'center',
          padding: '8px',
          color: '#555555',
          fontSize: '13px',
          marginBottom: '8px',
        }}>
          Loading detailed forecast data...
        </div>
      )}

      {/* Data Source Toggle */}
      <div style={{
        display: 'flex',
        justifyContent: 'center',
        gap: '8px',
        marginBottom: '16px',
      }}>
        {(['combined', 'markets', 'polling'] as DataSource[]).map((source) => {
          const isDisabled =
            (source === 'markets' && !hasMarketData) ||
            (source === 'polling' && !hasPollingData);

          return (
            <button
              key={source}
              onClick={() => !isDisabled && setDataSource(source)}
              disabled={isDisabled}
              style={{
                padding: '10px 20px',
                fontSize: '14px',
                fontWeight: dataSource === source ? 'bold' : 'normal',
                backgroundColor: dataSource === source ? '#6366f1' : isDisabled ? '#e5e7eb' : '#f3f4f6',
                color: dataSource === source ? 'white' : isDisabled ? '#888888' : '#333333',
                border: 'none',
                borderRadius: '8px',
                cursor: isDisabled ? 'not-allowed' : 'pointer',
                transition: 'all 0.2s ease',
                opacity: isDisabled ? 0.6 : 1,
              }}
              title={isDisabled ? `No ${source === 'markets' ? 'market' : 'polling'} data available` : ''}
            >
              {getSourceLabel(source)}
              {source === 'markets' && hasMarketData && (
                <span style={{ marginLeft: '6px', fontSize: '10px', opacity: 0.8 }}>
                  ({marketCount})
                </span>
              )}
              {source === 'polling' && hasPollingData && (
                <span style={{ marginLeft: '6px', fontSize: '10px', opacity: 0.8 }}>
                  ({pollingCount})
                </span>
              )}
            </button>
          );
        })}
      </div>

      {/* Victory Odds - hide for governors */}
      {raceType !== RaceType.Governor && (
        <div style={{ marginBottom: '32px' }}>
          <h3 style={{ margin: '0 0 8px 0', textAlign: 'center' }}>
            {chamberName} Forecast - Chance of Winning Majority
          </h3>
          <div style={{
            textAlign: 'center',
            fontSize: '13px',
            color: activeSource === 'markets' ? '#059669' : activeSource === 'polling' ? '#2563eb' : '#555555',
            marginBottom: '16px',
            fontWeight: 500,
          }}>
            {activeSource === 'markets' && 'Based on Polymarket prediction market odds'}
            {activeSource === 'polling' && 'Based on polling averages'}
            {activeSource === 'combined' && 'Combined forecast (markets + polling + fundamentals)'}
            {dataSource !== activeSource && dataSource !== 'combined' && (
              <span style={{ color: '#888888', fontStyle: 'italic', marginLeft: '8px' }}>
                (insufficient {dataSource === 'markets' ? 'market' : 'polling'} data - using combined)
              </span>
            )}
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
            {/* Democrat odds */}
            <div style={{ flex: 1, textAlign: 'center' }}>
              <div style={{ fontSize: '42px', fontWeight: 'bold', color: '#0033AA' }}>
                {demVictoryOdds}%
              </div>
              <div style={{ fontSize: '16px', color: '#666' }}>Democrats</div>
            </div>

            {/* Visual bar */}
            <div style={{ flex: 2, height: '48px', display: 'flex', borderRadius: '8px', overflow: 'hidden' }}>
              <div style={{
                width: `${demVictoryOdds}%`,
                backgroundColor: '#0033AA',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: 'white',
                fontWeight: 'bold',
                fontSize: '16px',
                transition: 'width 0.3s ease',
              }}>
                {demVictoryOdds > 20 && 'D'}
              </div>
              <div style={{
                width: `${repVictoryOdds}%`,
                backgroundColor: '#AA0000',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                color: 'white',
                fontWeight: 'bold',
                fontSize: '16px',
                transition: 'width 0.3s ease',
              }}>
                {repVictoryOdds > 20 && 'R'}
              </div>
            </div>

            {/* Republican odds */}
            <div style={{ flex: 1, textAlign: 'center' }}>
              <div style={{ fontSize: '42px', fontWeight: 'bold', color: '#AA0000' }}>
                {repVictoryOdds}%
              </div>
              <div style={{ fontSize: '16px', color: '#666' }}>Republicans</div>
            </div>
          </div>
        </div>
      )}

      {/* Seat Projections */}
      <div style={{ marginBottom: '32px' }}>
        <h3 style={{ margin: '0 0 16px 0', textAlign: 'center' }}>
          Projected Seats
        </h3>

        {/* Labels above bar */}
        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '6px' }}>
          <span style={{ fontSize: '22px', fontWeight: 'bold', color: '#0033AA' }}>{totalDemSeats}</span>
          <span style={{ fontSize: '22px', fontWeight: 'bold', color: '#AA0000' }}>{totalRepSeats}</span>
        </div>

        {/* Seat bar */}
        <div style={{
          height: '32px',
          display: 'flex',
          borderRadius: '6px',
          overflow: 'hidden',
          position: 'relative',
        }}>
          {seatSegments.map(seg => (
            <div key={seg.rating} style={{
              width: `${(seg.count / totalSeats) * 100}%`,
              backgroundColor: seg.color,
              transition: 'width 0.3s ease',
            }} />
          ))}
          {/* Majority line */}
          {raceType !== RaceType.Governor && (
            <div style={{
              position: 'absolute',
              left: `${(majorityNeeded / totalSeats) * 100}%`,
              top: 0,
              bottom: 0,
              width: '3px',
              backgroundColor: '#333',
            }} />
          )}
        </div>
      </div>
    </div>
  );
};
