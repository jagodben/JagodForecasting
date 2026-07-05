import { useState, useMemo } from 'react';
import { ComposableMap, Geographies, Geography } from 'react-simple-maps';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { StateSummary, Race, RaceRating, RaceType } from '../../types';
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
  if (demProb >= 0.90) return RaceRating.SolidDem;
  if (demProb >= 0.70) return RaceRating.LikelyDem;
  if (demProb >= 0.55) return RaceRating.LeanDem;
  if (demProb > 0.50) return RaceRating.TiltDem;
  if (demProb >= 0.45) return RaceRating.TiltRep;
  if (demProb >= 0.30) return RaceRating.LeanRep;
  if (demProb >= 0.10) return RaceRating.LikelyRep;
  return RaceRating.SolidRep;
};

const getRatingColor = (rating: RaceRating): string => {
  switch (rating) {
    case RaceRating.SolidDem: return '#0033AA';
    case RaceRating.LikelyDem: return '#2266DD';
    case RaceRating.LeanDem: return '#5599EE';
    case RaceRating.TiltDem: return '#99CCFF';
    case RaceRating.TiltRep: return '#FFCC99';
    case RaceRating.LeanRep: return '#E07070';
    case RaceRating.LikelyRep: return '#DD4422';
    case RaceRating.SolidRep: return '#AA0000';
    default: return '#E0E0E0';
  }
};

const getRatingLabel = (rating: RaceRating): string => {
  switch (rating) {
    case RaceRating.SolidDem: return 'Solid D';
    case RaceRating.LikelyDem: return 'Likely D';
    case RaceRating.LeanDem: return 'Lean D';
    case RaceRating.TiltDem: return 'Tilt D';
    case RaceRating.TiltRep: return 'Tilt R';
    case RaceRating.LeanRep: return 'Lean R';
    case RaceRating.LikelyRep: return 'Likely R';
    case RaceRating.SolidRep: return 'Solid R';
    default: return 'Unknown';
  }
};

export interface SelectedStateData {
  stateName: string;
  stateId: string;
  raceType: string;
  rating: RaceRating | null;
  demProb: number | null;
}

interface RaceMapProps {
  states: StateSummary[];
  races: Race[];
  raceType: RaceType;
  dataSource?: DataSource;
  onStateSelect?: (data: SelectedStateData | null) => void;
}

export { getRatingColor, getRatingLabel };

interface TooltipData {
  stateName: string;
  stateId: string;
  race: Race | null;
  demProb: number | null;
  rating: RaceRating | null;
}

export const RaceMap = ({ states, races, raceType, dataSource = 'combined', onStateSelect }: RaceMapProps) => {
  const navigate = useNavigate();
  const [tooltipData, setTooltipData] = useState<TooltipData | null>(null);
  const [tooltipPosition, setTooltipPosition] = useState({ x: 0, y: 0 });
  const raceTypeLabel = raceType === RaceType.Senate ? 'Senate' : 'Governor';

  // Fetch detailed forecasts for all races of this type in a single batched request
  // (shares its cache with the sidebar's ChamberForecast via the same query key).
  const { data: detailedForecasts } = useQuery({
    queryKey: ['forecasts', raceType],
    queryFn: () => forecastApi.getAll(raceType),
    enabled: races.length > 0,
  });

  // Calculate ratings based on selected data source
  const raceRatings = useMemo(() => {
    const ratings = new Map<string, { rating: RaceRating; demProb: number }>();

    races.forEach(race => {
      const detailed = detailedForecasts?.find(f => f.raceId === race.id);
      let demProb: number;

      // Get probability based on selected data source
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

      ratings.set(race.id, {
        rating: probabilityToRating(demProb),
        demProb,
      });
    });

    return ratings;
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

      // Notify parent for mobile display
      if (onStateSelect) {
        onStateSelect({
          stateName: state.name,
          stateId: stateId,
          raceType: raceTypeLabel,
          rating: ratingData?.rating ?? null,
          demProb: ratingData?.demProb ?? null,
        });
      }
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
      // Find the race for this state and navigate to the race page
      const race = races.find(r => r.stateId.toLowerCase() === stateId.toLowerCase());
      if (race) {
        navigate(`/race/${race.id}`);
      } else {
        navigate(`/state/${stateId}`);
      }
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

      {tooltipData && (
        <div
          className="map-tooltip"
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
                    <span style={{ color: '#0044CC', fontWeight: 500 }}>Democrat</span>
                    <span style={{ fontWeight: 'bold' }}>{(tooltipData.demProb * 100).toFixed(1)}%</span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span style={{ color: '#CC0000', fontWeight: 500 }}>Republican</span>
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
