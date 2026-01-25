import { useState, useMemo } from 'react';
import { ComposableMap, Geographies, Geography } from 'react-simple-maps';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { StateSummary, Race, RaceRating, RaceType, DetailedForecast } from '../../types';
import { forecastApi } from '../../services/api';

const GEO_URL = 'https://cdn.jsdelivr.net/npm/us-atlas@3/states-10m.json';

type DataSource = 'combined' | 'markets' | 'polling';

// Map state names to their abbreviations
const stateNameToId: Record<string, string> = {
  'Alabama': 'AL', 'Alaska': 'AK', 'Arizona': 'AZ', 'Arkansas': 'AR', 'California': 'CA',
  'Colorado': 'CO', 'Connecticut': 'CT', 'Delaware': 'DE', 'Florida': 'FL', 'Georgia': 'GA',
  'Hawaii': 'HI', 'Idaho': 'ID', 'Illinois': 'IL', 'Indiana': 'IN', 'Iowa': 'IA',
  'Kansas': 'KS', 'Kentucky': 'KY', 'Louisiana': 'LA', 'Maine': 'ME', 'Maryland': 'MD',
  'Massachusetts': 'MA', 'Michigan': 'MI', 'Minnesota': 'MN', 'Mississippi': 'MS', 'Missouri': 'MO',
  'Montana': 'MT', 'Nebraska': 'NE', 'Nevada': 'NV', 'New Hampshire': 'NH', 'New Jersey': 'NJ',
  'New Mexico': 'NM', 'New York': 'NY', 'North Carolina': 'NC', 'North Dakota': 'ND', 'Ohio': 'OH',
  'Oklahoma': 'OK', 'Oregon': 'OR', 'Pennsylvania': 'PA', 'Rhode Island': 'RI', 'South Carolina': 'SC',
  'South Dakota': 'SD', 'Tennessee': 'TN', 'Texas': 'TX', 'Utah': 'UT', 'Vermont': 'VT',
  'Virginia': 'VA', 'Washington': 'WA', 'West Virginia': 'WV', 'Wisconsin': 'WI', 'Wyoming': 'WY'
};

// Convert probability to rating
const probabilityToRating = (demProb: number): RaceRating => {
  if (demProb >= 0.95) return RaceRating.SolidDem;
  if (demProb >= 0.75) return RaceRating.LikelyDem;
  if (demProb >= 0.55) return RaceRating.LeanDem;
  if (demProb >= 0.45) return RaceRating.Tossup;
  if (demProb >= 0.25) return RaceRating.LeanRep;
  if (demProb >= 0.05) return RaceRating.LikelyRep;
  return RaceRating.SolidRep;
};

const getRatingColor = (rating: RaceRating): string => {
  switch (rating) {
    case RaceRating.SolidDem: return '#0015BC';
    case RaceRating.LikelyDem: return '#3355DD';
    case RaceRating.LeanDem: return '#7799EE';
    case RaceRating.Tossup: return '#9966CC';
    case RaceRating.LeanRep: return '#EE7777';
    case RaceRating.LikelyRep: return '#DD3333';
    case RaceRating.SolidRep: return '#BC0000';
    default: return '#CCCCCC';
  }
};

const getRatingLabel = (rating: RaceRating): string => {
  switch (rating) {
    case RaceRating.SolidDem: return 'Solid D';
    case RaceRating.LikelyDem: return 'Likely D';
    case RaceRating.LeanDem: return 'Lean D';
    case RaceRating.Tossup: return 'Tossup';
    case RaceRating.LeanRep: return 'Lean R';
    case RaceRating.LikelyRep: return 'Likely R';
    case RaceRating.SolidRep: return 'Solid R';
    default: return 'Unknown';
  }
};

interface RaceMapProps {
  states: StateSummary[];
  races: Race[];
  raceType: RaceType;
}

interface TooltipData {
  stateName: string;
  stateId: string;
  race: Race | null;
  demProb: number | null;
  rating: RaceRating | null;
}

export const RaceMap = ({ states, races, raceType }: RaceMapProps) => {
  const navigate = useNavigate();
  const [tooltipData, setTooltipData] = useState<TooltipData | null>(null);
  const [tooltipPosition, setTooltipPosition] = useState({ x: 0, y: 0 });
  const [dataSource, setDataSource] = useState<DataSource>('combined');

  // Fetch detailed forecasts for all races
  const { data: detailedForecasts } = useQuery({
    queryKey: ['forecasts', races.map(r => r.id)],
    queryFn: async () => {
      const forecasts = await Promise.all(
        races.map(race => forecastApi.getByRaceId(race.id).catch(() => null))
      );
      return forecasts.filter((f): f is DetailedForecast => f !== null);
    },
    enabled: races.length > 0,
  });

  // Calculate ratings based on selected data source
  const { raceRatings, hasMarketData, hasPollingData, marketCount, pollingCount } = useMemo(() => {
    const ratings = new Map<string, { rating: RaceRating; demProb: number }>();
    let marketsAvailable = 0;
    let pollingAvailable = 0;

    races.forEach(race => {
      const detailed = detailedForecasts?.find(f => f.raceId === race.id);
      let demProb: number;

      // Check data availability
      if (detailed?.inputs.marketOdds != null) marketsAvailable++;
      if (detailed?.inputs.pollingAverage != null) pollingAvailable++;

      // Get probability based on selected data source
      if (dataSource === 'markets' && detailed?.inputs.marketOdds != null) {
        demProb = detailed.inputs.marketOdds;
      } else if (dataSource === 'polling' && detailed?.inputs.pollingAverage != null) {
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

      ratings.set(race.id, {
        rating: probabilityToRating(demProb),
        demProb,
      });
    });

    return {
      raceRatings: ratings,
      hasMarketData: marketsAvailable > 0,
      hasPollingData: pollingAvailable > 0,
      marketCount: marketsAvailable,
      pollingCount: pollingAvailable,
    };
  }, [races, detailedForecasts, dataSource]);

  const stateMap = new Map(states.map(s => [s.id, s]));
  const raceMap = new Map(races.map(r => [r.stateId, r]));

  const handleMouseEnter = (geo: { properties: { name: string } }, event: React.MouseEvent) => {
    const stateName = geo.properties.name;
    const stateId = stateNameToId[stateName];
    const state = stateMap.get(stateId);
    const race = raceMap.get(stateId);
    const ratingData = race ? raceRatings.get(race.id) : null;

    if (state) {
      setTooltipData({
        stateName: state.name,
        stateId: stateId,
        race: race || null,
        demProb: ratingData?.demProb ?? null,
        rating: ratingData?.rating ?? null,
      });
      setTooltipPosition({ x: event.clientX, y: event.clientY });
    }
  };

  const handleMouseMove = (event: React.MouseEvent) => {
    setTooltipPosition({ x: event.clientX, y: event.clientY });
  };

  const handleMouseLeave = () => {
    setTooltipData(null);
  };

  const handleClick = (geo: { properties: { name: string } }) => {
    const stateName = geo.properties.name;
    const stateId = stateNameToId[stateName];
    if (stateId) {
      navigate(`/state/${stateId}`);
    }
  };

  const raceTypeLabel = raceType === RaceType.Senate ? 'Senate' : 'Governor';

  const getSourceLabel = (source: DataSource) => {
    switch (source) {
      case 'combined': return 'Combined';
      case 'markets': return 'Polymarket';
      case 'polling': return 'Polls';
    }
  };

  return (
    <div className="us-map-container" style={{ position: 'relative' }}>
      <ComposableMap projection="geoAlbersUsa" projectionConfig={{ scale: 1000 }}>
        <Geographies geography={GEO_URL}>
            {({ geographies }) => {
              const hoveredStateId = tooltipData?.race ? tooltipData.stateId : null;

              return (
                <>
                  {geographies.map((geo) => {
                    const stateName = geo.properties.name;
                    const stateId = stateNameToId[stateName];

                    if (stateId === hoveredStateId) return null;

                    const race = raceMap.get(stateId);
                    const ratingData = race ? raceRatings.get(race.id) : null;
                    const fillColor = ratingData ? getRatingColor(ratingData.rating) : (race ? getRatingColor(race.rating) : '#DDDDDD');

                    return (
                      <Geography
                        key={geo.rsmKey}
                        geography={geo}
                        fill={fillColor}
                        stroke="#FFFFFF"
                        strokeWidth={0.5}
                        style={{
                          default: { outline: 'none' },
                          hover: { outline: 'none', cursor: 'pointer' },
                          pressed: { outline: 'none' },
                        }}
                        onMouseEnter={(e) => handleMouseEnter(geo, e)}
                        onMouseMove={handleMouseMove}
                        onMouseLeave={handleMouseLeave}
                        onClick={() => handleClick(geo)}
                      />
                    );
                  })}
                  {hoveredStateId && geographies.map((geo) => {
                    const stateName = geo.properties.name;
                    const stateId = stateNameToId[stateName];

                    if (stateId !== hoveredStateId) return null;

                    const race = raceMap.get(stateId);
                    const ratingData = race ? raceRatings.get(race.id) : null;
                    const fillColor = ratingData ? getRatingColor(ratingData.rating) : (race ? getRatingColor(race.rating) : '#DDDDDD');

                    return (
                      <Geography
                        key={`hover-${geo.rsmKey}`}
                        geography={geo}
                        fill={fillColor}
                        stroke="#333"
                        strokeWidth={1.5}
                        style={{
                          default: {
                            outline: 'none',
                            transform: 'scale(1.05)',
                            transformOrigin: 'center',
                            transformBox: 'fill-box',
                            filter: 'drop-shadow(0 3px 6px rgba(0,0,0,0.4))',
                          },
                          hover: {
                            outline: 'none',
                            cursor: 'pointer',
                            transform: 'scale(1.05)',
                            transformOrigin: 'center',
                            transformBox: 'fill-box',
                            filter: 'drop-shadow(0 3px 6px rgba(0,0,0,0.4))',
                          },
                          pressed: { outline: 'none' },
                        }}
                        onMouseEnter={(e) => handleMouseEnter(geo, e)}
                        onMouseMove={handleMouseMove}
                        onMouseLeave={handleMouseLeave}
                        onClick={() => handleClick(geo)}
                      />
                    );
                  })}
                </>
              );
            }}
        </Geographies>
      </ComposableMap>

      {/* Data Source Tabs - Bottom Right */}
      <div style={{
        position: 'absolute',
        bottom: '12px',
        right: '12px',
        display: 'flex',
        gap: '4px',
        backgroundColor: 'rgba(255, 255, 255, 0.95)',
        padding: '4px',
        borderRadius: '6px',
        boxShadow: '0 2px 8px rgba(0,0,0,0.15)',
      }}>
        {(['combined', 'markets', 'polling'] as DataSource[]).map((source) => {
          const isDisabled =
            (source === 'markets' && !hasMarketData) ||
            (source === 'polling' && !hasPollingData);
          const isActive = dataSource === source;
          const count = source === 'markets' ? marketCount : source === 'polling' ? pollingCount : null;

          return (
            <button
              key={source}
              onClick={() => !isDisabled && setDataSource(source)}
              disabled={isDisabled}
              style={{
                padding: '6px 10px',
                fontSize: '11px',
                fontWeight: isActive ? 600 : 400,
                backgroundColor: isActive ? '#6366f1' : isDisabled ? '#e5e7eb' : '#f3f4f6',
                color: isActive ? 'white' : isDisabled ? '#9ca3af' : '#374151',
                border: 'none',
                borderRadius: '4px',
                cursor: isDisabled ? 'not-allowed' : 'pointer',
                opacity: isDisabled ? 0.6 : 1,
                whiteSpace: 'nowrap',
              }}
              title={isDisabled ? `No ${source === 'markets' ? 'market' : 'polling'} data available` : ''}
            >
              {getSourceLabel(source)}
              {count !== null && count > 0 && (
                <span style={{ marginLeft: '3px', opacity: 0.8 }}>({count})</span>
              )}
            </button>
          );
        })}
      </div>

      {tooltipData && (
        <div
          style={{
            position: 'fixed',
            left: tooltipPosition.x + 15,
            top: tooltipPosition.y + 15,
            backgroundColor: 'rgba(255, 255, 255, 0.98)',
            border: '1px solid #ccc',
            borderRadius: '8px',
            padding: '12px',
            boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
            pointerEvents: 'none',
            zIndex: 1000,
            minWidth: '220px',
            maxWidth: '300px',
          }}
        >
          <h4 style={{ margin: '0 0 8px 0', fontSize: '14px', borderBottom: '1px solid #eee', paddingBottom: '8px' }}>
            {tooltipData.stateName}
          </h4>

          {tooltipData.race ? (
            <div>
              <div style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                marginBottom: '8px',
              }}>
                <span style={{ fontWeight: 500 }}>{raceTypeLabel} Race</span>
                <span style={{
                  backgroundColor: tooltipData.rating ? getRatingColor(tooltipData.rating) : getRatingColor(tooltipData.race.rating),
                  color: 'white',
                  padding: '2px 8px',
                  borderRadius: '4px',
                  fontSize: '12px',
                  fontWeight: 'bold',
                }}>
                  {tooltipData.rating ? getRatingLabel(tooltipData.rating) : getRatingLabel(tooltipData.race.rating)}
                </span>
              </div>

              {tooltipData.demProb !== null && (
                <div style={{ marginBottom: '8px', fontSize: '13px' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span style={{ color: '#0015BC', fontWeight: 500 }}>Democrat</span>
                    <span style={{ fontWeight: 'bold' }}>{(tooltipData.demProb * 100).toFixed(1)}%</span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span style={{ color: '#BC0000', fontWeight: 500 }}>Republican</span>
                    <span style={{ fontWeight: 'bold' }}>{((1 - tooltipData.demProb) * 100).toFixed(1)}%</span>
                  </div>
                </div>
              )}
            </div>
          ) : (
            <div style={{ color: '#666', fontSize: '13px' }}>
              No {raceTypeLabel.toLowerCase()} race in 2026
            </div>
          )}
        </div>
      )}
    </div>
  );
};
