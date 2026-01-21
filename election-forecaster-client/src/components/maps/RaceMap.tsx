import { useState } from 'react';
import { ComposableMap, Geographies, Geography } from 'react-simple-maps';
import { useNavigate } from 'react-router-dom';
import { StateSummary, Race, RaceRating, RaceType } from '../../types';

const GEO_URL = 'https://cdn.jsdelivr.net/npm/us-atlas@3/states-10m.json';

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
}

export const RaceMap = ({ states, races, raceType }: RaceMapProps) => {
  const navigate = useNavigate();
  const [tooltipData, setTooltipData] = useState<TooltipData | null>(null);
  const [tooltipPosition, setTooltipPosition] = useState({ x: 0, y: 0 });

  const stateMap = new Map(states.map(s => [s.id, s]));
  const raceMap = new Map(races.map(r => [r.stateId, r]));

  const handleMouseEnter = (geo: { properties: { name: string } }, event: React.MouseEvent) => {
    const stateName = geo.properties.name;
    const stateId = stateNameToId[stateName];
    const state = stateMap.get(stateId);
    const race = raceMap.get(stateId);

    if (state) {
      setTooltipData({
        stateName: state.name,
        stateId: stateId,
        race: race || null,
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

  return (
    <div className="us-map-container">
      <ComposableMap projection="geoAlbersUsa" projectionConfig={{ scale: 1000 }}>
        <Geographies geography={GEO_URL}>
            {({ geographies }) => {
              // Only apply pop-out effect if hovered state has a race
              const hoveredStateId = tooltipData?.race ? tooltipData.stateId : null;

              return (
                <>
                  {/* Render non-hovered states first */}
                  {geographies.map((geo) => {
                    const stateName = geo.properties.name;
                    const stateId = stateNameToId[stateName];

                    if (stateId === hoveredStateId) return null;

                    const race = raceMap.get(stateId);
                    const fillColor = race ? getRatingColor(race.rating) : '#DDDDDD';

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
                  {/* Render hovered state on top (only if it has a race) */}
                  {hoveredStateId && geographies.map((geo) => {
                    const stateName = geo.properties.name;
                    const stateId = stateNameToId[stateName];

                    if (stateId !== hoveredStateId) return null;

                    const race = raceMap.get(stateId);
                    const fillColor = race ? getRatingColor(race.rating) : '#DDDDDD';

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
                  backgroundColor: getRatingColor(tooltipData.race.rating),
                  color: 'white',
                  padding: '2px 8px',
                  borderRadius: '4px',
                  fontSize: '12px',
                  fontWeight: 'bold',
                }}>
                  {getRatingLabel(tooltipData.race.rating)}
                </span>
              </div>

              <div style={{ display: 'flex', flexDirection: 'column', gap: '6px', fontSize: '13px' }}>
                {tooltipData.race.forecasts.map((forecast) => {
                  const candidate = tooltipData.race!.candidates.find(c => c.id === forecast.candidateId);
                  const isDemo = candidate?.party === 'Democrat';
                  return (
                    <div key={forecast.candidateId} style={{ display: 'flex', justifyContent: 'space-between', gap: '16px' }}>
                      <span style={{ color: isDemo ? '#0015BC' : '#BC0000', fontWeight: 500 }}>
                        {isDemo ? 'D' : 'R'}: {forecast.candidateName}
                      </span>
                      <span style={{ fontWeight: 'bold' }}>{(forecast.winProbability * 100).toFixed(0)}%</span>
                    </div>
                  );
                })}
              </div>
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
