import { useState, useMemo, useRef } from 'react';
import { ComposableMap, Geographies, Geography } from 'react-simple-maps';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { StateSummary, Race, RaceRating, RaceType, Party } from '../../types';
import { forecastApi } from '../../services/api';
import { ratingFill, MapPatternDefs } from './ratingFill';
import { useAccessibility } from '../../context/AccessibilityContext';
import { isTbdCandidate, TBD_NOTE } from '../../utils/candidates';

const GEO_URL = 'https://cdn.jsdelivr.net/npm/us-atlas@3/states-10m.json';

type DataSource = 'combined' | 'markets' | 'polling';

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

// When the challenger is a viable independent (not a Democrat), the "Dem-side" ratings render on a
// gold ramp instead of blue, so an independent-favored race reads as yellow on the map. The Rep side
// is unchanged. (Passing independent=false keeps the standard D↔R scale for every other race.)
const getRatingColor = (rating: RaceRating, independent = false): string => {
  if (independent) {
    switch (rating) {
      case RaceRating.SolidDem: return '#92660a';
      case RaceRating.LikelyDem: return '#b8860b';
      case RaceRating.LeanDem: return '#d4a017';
      case RaceRating.TiltDem: return '#ecc94b';
    }
  }
  switch (rating) {
    case RaceRating.SolidDem: return '#0044c9';
    case RaceRating.LikelyDem: return '#2d65d3';
    case RaceRating.LeanDem: return '#628cde';
    case RaceRating.TiltDem: return '#a5bdec';
    case RaceRating.TiltRep: return '#f7acaf';
    case RaceRating.LeanRep: return '#f06a70';
    case RaceRating.LikelyRep: return '#eb363d';
    case RaceRating.SolidRep: return '#e81b23';
    default: return '#E0E0E0';
  }
};

const getRatingLabel = (rating: RaceRating, independent = false): string => {
  if (independent) {
    switch (rating) {
      case RaceRating.SolidDem: return 'Solid I';
      case RaceRating.LikelyDem: return 'Likely I';
      case RaceRating.LeanDem: return 'Lean I';
      case RaceRating.TiltDem: return 'Tilt I';
    }
  }
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

// True when the race's challenger is a viable independent (the only Independent-party candidate we
// ever put in a race). Used to switch the map/tooltip to the gold "independent" styling.
const hasIndependentChallenger = (race: Race | null | undefined): boolean =>
  race?.candidates.some(c => c.party === Party.Independent) ?? false;

// Projected-result label + color for a race's expected Dem margin (points): D+/R+, or I+ (gold) when
// a viable independent is the one ahead. e.g. +5.3 → "D+5.3", -3 → "R+3", 0 → "EVEN".
const marginLabel = (margin: number, independentChallenger: boolean): { text: string; color: string } => {
  const rounded = Math.round(margin * 10) / 10;
  if (rounded === 0) return { text: 'EVEN', color: '#666' };
  const num = Number.isInteger(Math.abs(rounded)) ? Math.abs(rounded).toString() : Math.abs(rounded).toFixed(1);
  if (rounded > 0) {
    return independentChallenger ? { text: `I+${num}`, color: '#b8860b' } : { text: `D+${num}`, color: '#0044c9' };
  }
  return { text: `R+${num}`, color: '#e81b23' };
};

export interface SelectedStateData {
  stateName: string;
  stateId: string;
  raceType: string;
  rating: RaceRating | null;
  demProb: number | null;
  // Projected result (e.g. "D+5.3" / "R+3" / "I+2"), pre-formatted with its color.
  marginText: string | null;
  marginColor: string;
  // Race id so the mobile preview card can link to the race page (null: no race, link to state).
  raceId: string | null;
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
  const { patterns } = useAccessibility();
  const [tooltipData, setTooltipData] = useState<TooltipData | null>(null);
  const [tooltipPosition, setTooltipPosition] = useState({ x: 0, y: 0 });
  const raceTypeLabel = raceType === RaceType.Senate ? 'Senate' : 'Governor';
  // Touch flow: the first tap previews a state (fills the mobile info card); tapping the SAME
  // state again opens its race page. Desktop clicks navigate immediately (hover already previews).
  // wasTouchRef marks touch interaction (a tap fires touchstart before its synthetic click);
  // lastTapRef holds the state previewed by the previous tap — set only by taps, not by the
  // synthetic mouseenter that precedes each tap's click.
  const wasTouchRef = useRef(false);
  const lastTapRef = useRef<string | null>(null);

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
        // Fallback to the baseline forecast (challenger = non-Republican, i.e. Democrat or independent).
        const demForecast = race.forecasts.find(f =>
          race.candidates.find(c => c.id === f.candidateId)?.party !== 'Republican'
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
        const margin = race ? detailedForecasts?.find(f => f.raceId === race.id)?.expectedDemMargin : null;
        const label = margin != null ? marginLabel(margin, hasIndependentChallenger(race)) : null;
        onStateSelect({
          stateName: state.name,
          stateId: stateId,
          raceType: raceTypeLabel,
          rating: ratingData?.rating ?? null,
          demProb: ratingData?.demProb ?? null,
          marginText: label?.text ?? null,
          marginColor: label?.color ?? '#666',
          raceId: race?.id ?? null,
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

  const handleClick = (geo: { properties: { name: string } }, event?: React.MouseEvent) => {
    const stateName = geo.properties.name;
    const stateId = stateNameToId[stateName];
    if (!stateId) return;

    // Touch: first tap previews (the tap's synthetic mouseenter already filled the mobile card);
    // only a second tap on the same state opens the race page.
    if (wasTouchRef.current && lastTapRef.current !== stateId) {
      lastTapRef.current = stateId;
      if (event) handleMouseEnter(geo, event); // ensure the card fills even if mouseenter was missed
      return;
    }

    // Find the race for this state and navigate to the race page. fromView lets the
    // destination's "Map" breadcrumb return to the tab the user came from.
    const fromView = raceType === RaceType.Governor ? 'governors' : 'senate';
    const race = races.find(r => r.stateId.toLowerCase() === stateId.toLowerCase());
    if (race) {
      navigate(`/race/${race.id}`, { state: { fromView } });
    } else {
      navigate(`/state/${stateId}`, { state: { fromView } });
    }
  };

  return (
    <div
      className="us-map-container"
      style={{ position: 'relative' }}
      onTouchStart={() => { wasTouchRef.current = true; }}
    >
      <ComposableMap projection="geoAlbersUsa" projectionConfig={{ scale: 1000 }}>
        <MapPatternDefs ns="race" colorOf={getRatingColor} />
        <Geographies geography={GEO_URL}>
            {({ geographies }) => {
              const hoveredStateId = tooltipData?.race ? tooltipData.stateId : null;

              return (
                <>
                  {geographies.map((geo) => {
                    const stateName = geo.properties.name;
                    const stateId = stateNameToId[stateName];

                    // Every state stays mounted (even the hovered one) so its mouseenter/mouseleave
                    // always fire. The scaled highlight is drawn by a separate, non-interactive
                    // overlay layer below — swapping the hovered node in/out used to drop the
                    // mouseleave and leave a state stuck highlighted.
                    const race = raceMap.get(stateId);
                    const ratingData = race ? raceRatings.get(race.id) : null;
                    const rating = ratingData?.rating ?? race?.rating ?? null;
                    const independent = hasIndependentChallenger(race);
                    const fillColor = !rating
                      ? '#DDDDDD'
                      : patterns
                        ? ratingFill('race', rating, getRatingColor(rating, independent))
                        : getRatingColor(rating, independent);

                    return (
                      <Geography
                        key={geo.rsmKey}
                        geography={geo}
                        fill={fillColor}
                        stroke="#FFFFFF"
                        strokeWidth={0.5}
                        role="button"
                        aria-label={`${stateName} — ${rating ? getRatingLabel(rating, independent) : 'no 2026 race'}. Open race details.`}
                        style={{
                          default: { outline: 'none' },
                          hover: { outline: 'none', cursor: 'pointer' },
                          pressed: { outline: 'none' },
                        }}
                        onMouseEnter={(e) => handleMouseEnter(geo, e)}
                        onMouseMove={handleMouseMove}
                        onMouseLeave={handleMouseLeave}
                        onClick={(e) => handleClick(geo, e)}
                        onKeyDown={(e: React.KeyboardEvent) => {
                          if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); handleClick(geo); }
                        }}
                      />
                    );
                  })}
                  {hoveredStateId && geographies.map((geo) => {
                    const stateName = geo.properties.name;
                    const stateId = stateNameToId[stateName];

                    if (stateId !== hoveredStateId) return null;

                    const race = raceMap.get(stateId);
                    const ratingData = race ? raceRatings.get(race.id) : null;
                    const rating = ratingData?.rating ?? race?.rating ?? null;
                    const independent = hasIndependentChallenger(race);
                    const fillColor = !rating
                      ? '#DDDDDD'
                      : patterns
                        ? ratingFill('race', rating, getRatingColor(rating, independent))
                        : getRatingColor(rating, independent);

                    // Decoration only — pointerEvents: none lets the base layer underneath keep
                    // ownership of hover/click, so events never get lost to this transient node.
                    const highlightStyle = {
                      outline: 'none',
                      transform: 'scale(1.05)',
                      transformOrigin: 'center',
                      transformBox: 'fill-box' as const,
                      filter: 'drop-shadow(0 3px 6px rgba(0,0,0,0.4))',
                      pointerEvents: 'none' as const,
                    };
                    return (
                      <Geography
                        key={`hover-${geo.rsmKey}`}
                        geography={geo}
                        fill={fillColor}
                        stroke="#333"
                        strokeWidth={1.5}
                        style={{
                          default: highlightStyle,
                          hover: highlightStyle,
                          pressed: highlightStyle,
                        }}
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
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: '12px', margin: '0 0 8px 0', borderBottom: '1px solid #eee', paddingBottom: '8px' }}>
            <h4 style={{ margin: 0, fontSize: '14px' }}>{tooltipData.stateName}</h4>
            {(() => {
              // Projected result (D+/R+/I+) for the hovered race, from the blended forecast's margin.
              const race = tooltipData.race;
              const margin = race ? detailedForecasts?.find(f => f.raceId === race.id)?.expectedDemMargin : null;
              if (margin == null) return null;
              const { text, color } = marginLabel(margin, hasIndependentChallenger(race));
              return <span style={{ fontSize: '14px', fontWeight: 700, whiteSpace: 'nowrap', color }}>{text}</span>;
            })()}
          </div>

          {tooltipData.race ? (() => {
            const race = tooltipData.race!;
            const independent = hasIndependentChallenger(race);
            const rating = tooltipData.rating ?? race.rating;
            // Show actual candidate names (unresolved races carry the readable
            // "Democratic Nominee"/"Republican Nominee" placeholders). Independents in gold.
            const challengerName =
              race.candidates.find(c => c.party !== Party.Republican)?.name ?? 'Democrat';
            const repName =
              race.candidates.find(c => c.party === Party.Republican)?.name ?? 'Republican';
            const challengerColor = independent ? '#b8860b' : '#0044c9';
            return (
              <div>
                <div style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  marginBottom: '8px',
                }}>
                  <span style={{ fontWeight: 500 }}>{raceTypeLabel} Race</span>
                  <span style={{
                    backgroundColor: getRatingColor(rating, independent),
                    color: 'white',
                    padding: '2px 8px',
                    borderRadius: '4px',
                    fontSize: '12px',
                    fontWeight: 'bold',
                  }}>
                    {getRatingLabel(rating, independent)}
                  </span>
                </div>

                {tooltipData.demProb !== null && (
                  <div style={{ marginBottom: '8px', fontSize: '13px' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                      <span style={{ color: challengerColor, fontWeight: 500 }}>{challengerName}{isTbdCandidate(challengerName) && '*'}</span>
                      <span style={{ fontWeight: 'bold' }}>{(tooltipData.demProb * 100).toFixed(1)}%</span>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                      <span style={{ color: '#e81b23', fontWeight: 500 }}>{repName}{isTbdCandidate(repName) && '*'}</span>
                      <span style={{ fontWeight: 'bold' }}>{((1 - tooltipData.demProb) * 100).toFixed(1)}%</span>
                    </div>
                    {(isTbdCandidate(challengerName) || isTbdCandidate(repName)) && (
                      <div style={{ marginTop: '6px', fontSize: '11px', color: '#6b6b6b' }}>{TBD_NOTE}</div>
                    )}
                  </div>
                )}
              </div>
            );
          })() : (
            <div style={{ color: '#666', fontSize: '13px' }}>
              No {raceTypeLabel.toLowerCase()} race in 2026
            </div>
          )}
        </div>
      )}
    </div>
  );
};
