import { useMemo, useState, useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Race, RaceType, RaceRating, DetailedForecast } from '../../types';
import { forecastApi } from '../../services/api';

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

interface HistoricalOdds {
  date: string;
  demOdds: number;
}

// Mock historical data - in a real app this would come from the API
// Different data sources have different volatility and base patterns
const generateMockHistoricalData = (currentDemOdds: number, raceType: RaceType, dataSource: DataSource): HistoricalOdds[] => {
  const data: HistoricalOdds[] = [];
  const today = new Date();

  // Different starting points and volatility based on data source
  let baseOdds: number;
  let volatility: number;
  let trendStrength: number;

  switch (dataSource) {
    case 'markets':
      // Markets are more volatile but more responsive to news
      baseOdds = raceType === RaceType.Senate ? 45 : 42;
      volatility = 4; // Higher daily swings
      trendStrength = 0.08; // Faster trend adjustment
      break;
    case 'polling':
      // Polling is more stable but slower to change
      baseOdds = raceType === RaceType.Senate ? 50 : 46;
      volatility = 1.5; // Lower daily swings
      trendStrength = 0.03; // Slower trend adjustment
      break;
    case 'combined':
    default:
      // Combined is a middle ground
      baseOdds = raceType === RaceType.Senate ? 48 : 44;
      volatility = 2.5;
      trendStrength = 0.05;
      break;
  }

  let runningOdds = baseOdds;

  for (let i = 60; i >= 0; i--) {
    const date = new Date(today);
    date.setDate(date.getDate() - i);

    // Simulate daily model recalculation with random walk + trend toward current
    const randomShift = (Math.random() - 0.5) * volatility;
    const trendPull = (currentDemOdds - runningOdds) * trendStrength;

    runningOdds = runningOdds + randomShift + trendPull;
    runningOdds = Math.max(20, Math.min(80, runningOdds));

    data.push({
      date: date.toISOString().split('T')[0],
      demOdds: Math.round(runningOdds * 10) / 10,
    });
  }

  // Ensure the last point matches current odds exactly
  if (data.length > 0) {
    data[data.length - 1].demOdds = currentDemOdds;
  }

  return data;
};

export const ChamberForecast = ({ races, raceType, compact = false, dataSource: externalDataSource, onDataSourceChange, onDataAvailabilityChange }: ChamberForecastProps) => {
  const [internalDataSource, setInternalDataSource] = useState<DataSource>('combined');
  const dataSource = externalDataSource ?? internalDataSource;
  const setDataSource = onDataSourceChange ?? setInternalDataSource;

  // Fetch detailed forecasts for all races to get market/polling data
  const { data: detailedForecasts, isLoading: isLoadingForecasts } = useQuery({
    queryKey: ['forecasts', races.map(r => r.id)],
    queryFn: async () => {
      const forecasts = await Promise.all(
        races.map(race => forecastApi.getByRaceId(race.id).catch(() => null))
      );
      return forecasts.filter((f): f is DetailedForecast => f !== null);
    },
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

  const { seatProjection, seatsByRating, demVictoryOdds, historicalData, hasMarketData, hasPollingData, activeSource, marketCount, pollingCount } = useMemo(() => {
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
      } else if (dataSource === 'polling' && detailed?.inputs.pollingAverage != null) {
        // Convert polling percentage to probability (simplified)
        demProb = detailed.inputs.pollingAverage / 100;
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
        } else if (dataSource === 'polling' && detailed?.inputs.pollingAverage != null) {
          barRating = probabilityToRating(detailed.inputs.pollingAverage / 100);
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
      const pollingOdds = detailedForecasts
        .filter(f => f.inputs.pollingAverage != null)
        .map(f => f.inputs.pollingAverage!);
      if (pollingOdds.length > 0) {
        demOdds = Math.round((pollingOdds.reduce((a, b) => a + b, 0) / pollingOdds.length) * 10) / 10;
      } else {
        effectiveSource = 'combined';
        demOdds = calculateCombinedOdds(projection, races.length, raceType);
      }
    } else {
      demOdds = calculateCombinedOdds(projection, races.length, raceType);
    }

    demOdds = Math.max(5, Math.min(95, demOdds));

    const historical = generateMockHistoricalData(demOdds, raceType, effectiveSource);

    return {
      seatProjection: projection,
      seatsByRating: ratingCounts,
      demVictoryOdds: demOdds,
      historicalData: historical,
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

  const repVictoryOdds = Math.round((100 - demVictoryOdds) * 10) / 10;
  const chamberName = raceType === RaceType.Senate ? 'Senate' : raceType === RaceType.House ? 'House' : 'Governors';
  const totalSeats = raceType === RaceType.Senate ? 100 : raceType === RaceType.House ? 435 : 50;
  const majorityNeeded = raceType === RaceType.Senate ? 50 : raceType === RaceType.House ? 218 : 26;

  // For seats not up for election (simplified assumption)
  const seatsNotUp = totalSeats - races.length;
  const assumedDemHeld = Math.round(seatsNotUp * 0.48);
  const assumedRepHeld = seatsNotUp - assumedDemHeld;

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
              <span style={{ color: '#0044CC', fontWeight: 'bold', fontSize: '18px' }}>{demVictoryOdds}%</span>
              <span style={{ color: '#CC0000', fontWeight: 'bold', fontSize: '18px' }}>{repVictoryOdds}%</span>
            </div>
            <div className="forecast-sidebar__seat-bar">
              <div style={{ width: `${demVictoryOdds}%`, backgroundColor: '#0044CC' }} />
              <div style={{ width: `${repVictoryOdds}%`, backgroundColor: '#CC0000' }} />
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
            <span style={{ color: '#0044CC', fontWeight: 'bold', fontSize: '18px' }}>{totalDemSeats}</span>
            <span style={{ color: '#CC0000', fontWeight: 'bold', fontSize: '18px' }}>{totalRepSeats}</span>
          </div>
          <div className="forecast-sidebar__seat-bar">
            {seatSegments.map(seg => (
              <div key={seg.rating} style={{
                width: `${(seg.count / totalSeats) * 100}%`,
                backgroundColor: seg.color,
              }} />
            ))}
            <div className="forecast-sidebar__majority-line" style={{
              left: `${(majorityNeeded / totalSeats) * 100}%`,
            }} />
          </div>
          <div className="forecast-sidebar__majority-label">
            {majorityNeeded} needed for majority
          </div>
        </div>

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
          color: '#6b7280',
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
                color: dataSource === source ? 'white' : isDisabled ? '#9ca3af' : '#374151',
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
            color: activeSource === 'markets' ? '#059669' : activeSource === 'polling' ? '#2563eb' : '#6b7280',
            marginBottom: '16px',
            fontWeight: 500,
          }}>
            {activeSource === 'markets' && 'Based on Polymarket prediction market odds'}
            {activeSource === 'polling' && 'Based on polling averages'}
            {activeSource === 'combined' && 'Combined forecast (markets + polling + fundamentals)'}
            {dataSource !== activeSource && dataSource !== 'combined' && (
              <span style={{ color: '#9ca3af', fontStyle: 'italic', marginLeft: '8px' }}>
                (insufficient {dataSource === 'markets' ? 'market' : 'polling'} data - using combined)
              </span>
            )}
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
            {/* Democrat odds */}
            <div style={{ flex: 1, textAlign: 'center' }}>
              <div style={{ fontSize: '42px', fontWeight: 'bold', color: '#0044CC' }}>
                {demVictoryOdds}%
              </div>
              <div style={{ fontSize: '16px', color: '#666' }}>Democrats</div>
            </div>

            {/* Visual bar */}
            <div style={{ flex: 2, height: '48px', display: 'flex', borderRadius: '8px', overflow: 'hidden' }}>
              <div style={{
                width: `${demVictoryOdds}%`,
                backgroundColor: '#0044CC',
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
                backgroundColor: '#CC0000',
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
              <div style={{ fontSize: '42px', fontWeight: 'bold', color: '#CC0000' }}>
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
          <span style={{ fontSize: '22px', fontWeight: 'bold', color: '#0044CC' }}>{totalDemSeats}</span>
          <span style={{ fontSize: '22px', fontWeight: 'bold', color: '#CC0000' }}>{totalRepSeats}</span>
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
          <div style={{
            position: 'absolute',
            left: `${(majorityNeeded / totalSeats) * 100}%`,
            top: 0,
            bottom: 0,
            width: '3px',
            backgroundColor: '#333',
          }} />
        </div>
      </div>

      {/* Historical Trend Chart - hide for governors */}
      {raceType !== RaceType.Governor && (
        <div style={{ marginBottom: '32px' }}>
          <h3 style={{ margin: '0 0 8px 0', textAlign: 'center' }}>
            Win Probability Over Time
          </h3>
          <div style={{
            textAlign: 'center',
            fontSize: '13px',
            color: activeSource === 'markets' ? '#059669' : activeSource === 'polling' ? '#2563eb' : '#6b7280',
            marginBottom: '16px',
            fontWeight: 500,
          }}>
            {activeSource === 'markets' && 'Polymarket odds history'}
            {activeSource === 'polling' && 'Polling average history'}
            {activeSource === 'combined' && 'Combined forecast history'}
          </div>

          <OddsChart data={historicalData} />
        </div>
      )}
    </div>
  );
};

// SVG line chart component with both party lines
const OddsChart = ({ data }: { data: HistoricalOdds[] }) => {
  const [hoveredPoint, setHoveredPoint] = useState<{ index: number; party: 'dem' | 'rep' } | null>(null);

  const width = 900;
  const height = 400;
  const padding = { top: 35, right: 65, bottom: 45, left: 55 };

  const chartWidth = width - padding.left - padding.right;
  const chartHeight = height - padding.top - padding.bottom;

  // Scale functions
  const xScale = (index: number) => padding.left + (index / (data.length - 1)) * chartWidth;
  const yScale = (value: number) => padding.top + chartHeight - ((value / 100) * chartHeight);

  // Generate Democrat path
  const demLinePath = data.map((d, i) => {
    const x = xScale(i);
    const y = yScale(d.demOdds);
    return `${i === 0 ? 'M' : 'L'} ${x} ${y}`;
  }).join(' ');

  // Generate Republican path (100 - demOdds)
  const repLinePath = data.map((d, i) => {
    const x = xScale(i);
    const y = yScale(100 - d.demOdds);
    return `${i === 0 ? 'M' : 'L'} ${x} ${y}`;
  }).join(' ');

  // Get labels for x-axis (show dates at intervals)
  const dateIndices = [0, Math.floor(data.length / 4), Math.floor(data.length / 2), Math.floor(3 * data.length / 4), data.length - 1];
  const dateLabels = dateIndices.map(i => ({
    index: i,
    label: new Date(data[i].date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' }),
  }));

  const currentDemOdds = data[data.length - 1].demOdds;
  const currentRepOdds = Math.round((100 - currentDemOdds) * 10) / 10;

  // Get tooltip data
  const getTooltipData = () => {
    if (!hoveredPoint) return null;
    const d = data[hoveredPoint.index];
    const date = new Date(d.date).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    const odds = hoveredPoint.party === 'dem' ? d.demOdds : Math.round((100 - d.demOdds) * 10) / 10;
    const party = hoveredPoint.party === 'dem' ? 'Democrats' : 'Republicans';
    const color = hoveredPoint.party === 'dem' ? '#0044CC' : '#CC0000';
    const x = xScale(hoveredPoint.index);
    const y = yScale(hoveredPoint.party === 'dem' ? d.demOdds : 100 - d.demOdds);
    return { date, odds, party, color, x, y };
  };

  const tooltipData = getTooltipData();

  return (
    <svg width="100%" viewBox={`0 0 ${width} ${height}`} style={{ maxWidth: '100%' }}>
      {/* Grid lines */}
      {[20, 40, 60, 80].map(v => (
        <g key={v}>
          <line
            x1={padding.left}
            y1={yScale(v)}
            x2={width - padding.right}
            y2={yScale(v)}
            stroke="#eee"
            strokeWidth="1"
          />
          <text
            x={padding.left - 10}
            y={yScale(v)}
            textAnchor="end"
            alignmentBaseline="middle"
            fontSize="12"
            fill="#999"
          >
            {v}%
          </text>
        </g>
      ))}

      {/* 50% line (highlighted - the "toss-up" line) */}
      <line
        x1={padding.left}
        y1={yScale(50)}
        x2={width - padding.right}
        y2={yScale(50)}
        stroke="#666"
        strokeWidth="1.5"
        strokeDasharray="6,4"
      />
      <text
        x={width - padding.right + 8}
        y={yScale(50)}
        alignmentBaseline="middle"
        fontSize="11"
        fill="#666"
      >
        50%
      </text>

      {/* Data points and lines for Democrats */}
      <path
        d={demLinePath}
        fill="none"
        stroke="#0044CC"
        strokeWidth="3"
        strokeLinejoin="round"
      />
      {data.map((d, i) => (
        <circle
          key={`dem-${i}`}
          cx={xScale(i)}
          cy={yScale(d.demOdds)}
          r={hoveredPoint?.index === i && hoveredPoint?.party === 'dem' ? 8 : (i === data.length - 1 ? 7 : 2.5)}
          fill="#0044CC"
          style={{ cursor: 'pointer' }}
          onMouseEnter={() => setHoveredPoint({ index: i, party: 'dem' })}
          onMouseLeave={() => setHoveredPoint(null)}
        />
      ))}

      {/* Data points and lines for Republicans */}
      <path
        d={repLinePath}
        fill="none"
        stroke="#CC0000"
        strokeWidth="3"
        strokeLinejoin="round"
      />
      {data.map((d, i) => (
        <circle
          key={`rep-${i}`}
          cx={xScale(i)}
          cy={yScale(100 - d.demOdds)}
          r={hoveredPoint?.index === i && hoveredPoint?.party === 'rep' ? 8 : (i === data.length - 1 ? 7 : 2.5)}
          fill="#CC0000"
          style={{ cursor: 'pointer' }}
          onMouseEnter={() => setHoveredPoint({ index: i, party: 'rep' })}
          onMouseLeave={() => setHoveredPoint(null)}
        />
      ))}

      {/* X-axis labels */}
      {dateLabels.map(({ index, label }) => (
        <text
          key={index}
          x={xScale(index)}
          y={height - 12}
          textAnchor="middle"
          fontSize="12"
          fill="#666"
        >
          {label}
        </text>
      ))}

      {/* Current value labels */}
      <text
        x={width - padding.right + 8}
        y={yScale(currentDemOdds)}
        alignmentBaseline="middle"
        fontSize="14"
        fontWeight="bold"
        fill="#0044CC"
      >
        {currentDemOdds}%
      </text>
      <text
        x={width - padding.right + 8}
        y={yScale(currentRepOdds)}
        alignmentBaseline="middle"
        fontSize="14"
        fontWeight="bold"
        fill="#CC0000"
      >
        {currentRepOdds}%
      </text>

      {/* Legend */}
      <g transform={`translate(${padding.left + 10}, ${padding.top - 18})`}>
        <circle cx="0" cy="0" r="6" fill="#0044CC" />
        <text x="12" y="0" alignmentBaseline="middle" fontSize="13" fill="#333">Democrats</text>
        <circle cx="110" cy="0" r="6" fill="#CC0000" />
        <text x="122" y="0" alignmentBaseline="middle" fontSize="13" fill="#333">Republicans</text>
      </g>

      {/* Tooltip */}
      {tooltipData && (
        <g transform={`translate(${tooltipData.x}, ${tooltipData.y - 12})`}>
          <rect
            x="-50"
            y="-38"
            width="100"
            height="36"
            rx="6"
            fill="white"
            stroke={tooltipData.color}
            strokeWidth="2"
            filter="drop-shadow(0 2px 4px rgba(0,0,0,0.2))"
          />
          <text
            x="0"
            y="-22"
            textAnchor="middle"
            fontSize="11"
            fill="#666"
          >
            {tooltipData.date}
          </text>
          <text
            x="0"
            y="-6"
            textAnchor="middle"
            fontSize="15"
            fontWeight="bold"
            fill="#333"
          >
            {tooltipData.odds}%
          </text>
        </g>
      )}
    </svg>
  );
};
