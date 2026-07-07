import { useState, useEffect, useRef, useCallback, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Race, RaceRating, RaceType } from '../../types';
import { forecastApi } from '../../services/api';
import { geoPath, geoAlbersUsa, GeoPermissibleObjects } from 'd3-geo';
import { feature } from 'topojson-client';
import { ratingFill, MapPatternDefs } from './ratingFill';
import { useAccessibility } from '../../context/AccessibilityContext';

type DataSource = 'combined' | 'markets' | 'polling';

const DISTRICTS_URL = '/data/districts.json';

// State FIPS to abbreviation mapping
const fipsToState: Record<string, string> = {
  '01': 'AL', '02': 'AK', '04': 'AZ', '05': 'AR', '06': 'CA', '08': 'CO', '09': 'CT', '10': 'DE',
  '12': 'FL', '13': 'GA', '15': 'HI', '16': 'ID', '17': 'IL', '18': 'IN', '19': 'IA', '20': 'KS',
  '21': 'KY', '22': 'LA', '23': 'ME', '24': 'MD', '25': 'MA', '26': 'MI', '27': 'MN', '28': 'MS',
  '29': 'MO', '30': 'MT', '31': 'NE', '32': 'NV', '33': 'NH', '34': 'NJ', '35': 'NM', '36': 'NY',
  '37': 'NC', '38': 'ND', '39': 'OH', '40': 'OK', '41': 'OR', '42': 'PA', '44': 'RI', '45': 'SC',
  '46': 'SD', '47': 'TN', '48': 'TX', '49': 'UT', '50': 'VT', '51': 'VA', '53': 'WA', '54': 'WV',
  '55': 'WI', '56': 'WY', '11': 'DC', '72': 'PR'
};

// State names for tooltips
const stateNames: Record<string, string> = {
  'AL': 'Alabama', 'AK': 'Alaska', 'AZ': 'Arizona', 'AR': 'Arkansas', 'CA': 'California',
  'CO': 'Colorado', 'CT': 'Connecticut', 'DE': 'Delaware', 'FL': 'Florida', 'GA': 'Georgia',
  'HI': 'Hawaii', 'ID': 'Idaho', 'IL': 'Illinois', 'IN': 'Indiana', 'IA': 'Iowa',
  'KS': 'Kansas', 'KY': 'Kentucky', 'LA': 'Louisiana', 'ME': 'Maine', 'MD': 'Maryland',
  'MA': 'Massachusetts', 'MI': 'Michigan', 'MN': 'Minnesota', 'MS': 'Mississippi', 'MO': 'Missouri',
  'MT': 'Montana', 'NE': 'Nebraska', 'NV': 'Nevada', 'NH': 'New Hampshire', 'NJ': 'New Jersey',
  'NM': 'New Mexico', 'NY': 'New York', 'NC': 'North Carolina', 'ND': 'North Dakota', 'OH': 'Ohio',
  'OK': 'Oklahoma', 'OR': 'Oregon', 'PA': 'Pennsylvania', 'RI': 'Rhode Island', 'SC': 'South Carolina',
  'SD': 'South Dakota', 'TN': 'Tennessee', 'TX': 'Texas', 'UT': 'Utah', 'VT': 'Vermont',
  'VA': 'Virginia', 'WA': 'Washington', 'WV': 'West Virginia', 'WI': 'Wisconsin', 'WY': 'Wyoming',
  'DC': 'Washington D.C.', 'PR': 'Puerto Rico'
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

export interface SelectedDistrictData {
  stateName: string;
  stateId: string;
  districtNum: number;
  districtLabel: string;
  rating: RaceRating | null;
  demProb: number | null;
}

interface USDistrictMapProps {
  races: Race[];
  dataSource?: DataSource;
  onDistrictSelect?: (data: SelectedDistrictData | null) => void;
}

interface DistrictFeature {
  type: string;
  properties: {
    STATEFP: string;
    CD118FP: string;
    GEOID: string;
    NAMELSAD: string;
  };
  geometry: any;
}

interface TooltipData {
  stateId: string;
  districtNum: number;
  race: Race | null;
}

export const USDistrictMap = ({ races, dataSource = 'combined', onDistrictSelect }: USDistrictMapProps) => {
  const { patterns } = useAccessibility();
  const [tooltipData, setTooltipData] = useState<TooltipData | null>(null);
  const [tooltipPosition, setTooltipPosition] = useState({ x: 0, y: 0 });
  const [districtFeatures, setDistrictFeatures] = useState<DistrictFeature[]>([]);
  const [paths, setPaths] = useState<Map<string, string>>(new Map());

  // Zoom and pan state
  const [zoom, setZoom] = useState(1);
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const [isPanning, setIsPanning] = useState(false);
  const [lastMouse, setLastMouse] = useState({ x: 0, y: 0 });
  const svgRef = useRef<SVGSVGElement>(null);
  const zoomRef = useRef(zoom);
  const panRef = useRef(pan);

  // Keep refs in sync with state
  zoomRef.current = zoom;
  panRef.current = pan;

  // Fetch detailed forecasts for data-source-aware coloring in a single batched request
  // (one call for all House races instead of one per district).
  const { data: detailedForecasts } = useQuery({
    queryKey: ['forecasts', RaceType.House],
    queryFn: () => forecastApi.getAll(RaceType.House),
    enabled: races.length > 0,
  });

  // Calculate ratings based on selected data source
  // For 'combined', use the original per-district race.rating (detailed forecasts are state-level)
  const raceRatings = useMemo(() => {
    if (dataSource === 'combined') return null;

    const ratings = new Map<string, RaceRating>();
    races.forEach(race => {
      const detailed = detailedForecasts?.find(f => f.raceId === race.id);
      let demProb: number | null = null;

      if (dataSource === 'markets' && detailed?.inputs.marketOdds != null) {
        demProb = detailed.inputs.marketOdds;
      } else if (dataSource === 'polling' && detailed?.inputs.pollingWinProbability != null) {
        demProb = detailed.inputs.pollingWinProbability;
      }

      if (demProb != null) {
        ratings.set(race.id, probabilityToRating(demProb));
      }
    });
    return ratings;
  }, [races, detailedForecasts, dataSource]);

  // Create race lookup map: "stateId-districtNum" -> Race
  const raceMap = new Map(
    races.map(r => [`${r.stateId}-${r.districtNumber || 1}`, r])
  );

  // Load and process TopoJSON data
  useEffect(() => {
    fetch(DISTRICTS_URL)
      .then(res => res.json())
      .then(topology => {
        const objectName = Object.keys(topology.objects)[0];
        const geoJson = feature(topology, topology.objects[objectName]) as any;

        // Filter out territories we don't have race data for (keep all 50 states + DC)
        const validFips = new Set(Object.keys(fipsToState));
        const allDistricts = geoJson.features.filter((f: DistrictFeature) =>
          validFips.has(f.properties.STATEFP)
        );

        // Use Albers USA projection for the full US view
        const projection = geoAlbersUsa().scale(1000).translate([400, 250]);
        const pathGenerator = geoPath().projection(projection);

        const pathMap = new Map<string, string>();
        allDistricts.forEach((district: DistrictFeature) => {
          const pathData = pathGenerator(district as unknown as GeoPermissibleObjects);
          if (pathData) {
            pathMap.set(district.properties.GEOID, pathData);
          }
        });

        setDistrictFeatures(allDistricts);
        setPaths(pathMap);
      })
      .catch(err => console.error('Failed to load district data:', err));
  }, []);

  // Use callback ref to attach wheel event listener
  const svgCallbackRef = useCallback((node: SVGSVGElement | null) => {
    if (svgRef.current) {
      svgRef.current.removeEventListener('wheel', handleWheelRef.current);
    }

    svgRef.current = node;

    if (node) {
      node.addEventListener('wheel', handleWheelRef.current, { passive: false });
    }
  }, []);

  // Store wheel handler in ref to avoid recreating listener
  const handleWheelRef = useRef((e: WheelEvent) => {
    e.preventDefault();
    const svgElement = svgRef.current;
    if (!svgElement) return;

    const rect = svgElement.getBoundingClientRect();

    // Get mouse position relative to SVG element (0-1 range)
    const mouseX = (e.clientX - rect.left) / rect.width;
    const mouseY = (e.clientY - rect.top) / rect.height;

    // Convert to viewBox coordinates (0-800, 0-500)
    const viewBoxX = mouseX * 800;
    const viewBoxY = mouseY * 500;

    const oldZoom = zoomRef.current;
    const oldPan = panRef.current;
    const delta = e.deltaY > 0 ? 0.9 : 1.1;
    const newZoom = Math.min(Math.max(oldZoom * delta, 1), 10);

    // Calculate the point in map coordinates that's under the cursor
    // The transform is: translate(400 + pan.x, 250 + pan.y) scale(zoom) translate(-400, -250)
    // So a viewBox point (vx, vy) maps to map point: ((vx - 400 - pan.x) / zoom + 400, (vy - 250 - pan.y) / zoom + 250)
    const mapX = (viewBoxX - 400 - oldPan.x) / oldZoom + 400;
    const mapY = (viewBoxY - 250 - oldPan.y) / oldZoom + 250;

    // Calculate new pan to keep the same map point under the cursor
    // We want: (viewBoxX - 400 - newPan.x) / newZoom + 400 = mapX
    // Solving: newPan.x = viewBoxX - 400 - (mapX - 400) * newZoom
    const newPanX = viewBoxX - 400 - (mapX - 400) * newZoom;
    const newPanY = viewBoxY - 250 - (mapY - 250) * newZoom;

    // Clamp pan values
    const maxPanX = newZoom > 1 ? 400 * newZoom * 0.8 : 0;
    const maxPanY = newZoom > 1 ? 250 * newZoom * 0.8 : 0;

    setZoom(newZoom);
    setPan({
      x: Math.max(-maxPanX, Math.min(maxPanX, newPanX)),
      y: Math.max(-maxPanY, Math.min(maxPanY, newPanY))
    });
  });

  const handleMouseDown = useCallback((e: React.MouseEvent) => {
    if (e.button === 0) {
      e.preventDefault();
      setIsPanning(true);
      setLastMouse({ x: e.clientX, y: e.clientY });
    }
  }, []);

  const handleMouseMove = useCallback((e: React.MouseEvent) => {
    setTooltipPosition({ x: e.clientX, y: e.clientY });

    if (isPanning) {
      const dx = e.clientX - lastMouse.x;
      const dy = e.clientY - lastMouse.y;

      const svgElement = svgRef.current;
      if (svgElement) {
        const rect = svgElement.getBoundingClientRect();
        const scaleX = 800 / rect.width;
        const scaleY = 500 / rect.height;

        const maxPanX = zoom > 1 ? 400 * zoom * 0.8 : 0;
        const maxPanY = zoom > 1 ? 250 * zoom * 0.8 : 0;

        setPan(p => ({
          x: Math.max(-maxPanX, Math.min(maxPanX, p.x + dx * scaleX)),
          y: Math.max(-maxPanY, Math.min(maxPanY, p.y + dy * scaleY))
        }));
      }

      setLastMouse({ x: e.clientX, y: e.clientY });
    }
  }, [isPanning, lastMouse, zoom]);

  const handleMouseUp = useCallback(() => {
    setIsPanning(false);
  }, []);

  const handleMouseLeave = useCallback(() => {
    setIsPanning(false);
    setTooltipData(null);
  }, []);

  const handleDistrictMouseEnter = (stateId: string, districtNum: number) => {
    if (isPanning) return;
    const race = raceMap.get(`${stateId}-${districtNum}`);
    setTooltipData({
      stateId,
      districtNum,
      race: race || null,
    });
  };

  const handleDistrictMouseLeave = () => {
    if (!isPanning) {
      setTooltipData(null);
    }
  };

  const handleDistrictClick = (stateId: string, districtNum: number) => {
    if (isPanning) return;
    // Find the House race for this district
    const race = races.find(r =>
      r.stateId.toLowerCase() === stateId.toLowerCase() &&
      r.districtNumber === districtNum
    );

    // Call onDistrictSelect callback if provided
    if (onDistrictSelect) {
      if (race) {
        const detailed = detailedForecasts?.find(f => f.raceId === race.id);
        let demProb: number | null = null;

        if (dataSource === 'markets' && detailed?.inputs.marketOdds != null) {
          demProb = detailed.inputs.marketOdds;
        } else if (dataSource === 'polling' && detailed?.inputs.pollingWinProbability != null) {
          demProb = detailed.inputs.pollingWinProbability;
        } else {
          // Get dem probability from forecasts
          const demForecast = race.forecasts.find(f => {
            const candidate = race.candidates.find(c => c.id === f.candidateId);
            return candidate?.party === 'Democrat';
          });
          demProb = demForecast?.winProbability ?? null;
        }

        const rating = raceRatings?.get(race.id) ?? race.rating;
        const districtLabel = districtNum === 1 && !race.districtNumber
          ? 'At-Large'
          : `District ${districtNum}`;

        onDistrictSelect({
          stateName: stateNames[stateId] || stateId,
          stateId,
          districtNum,
          districtLabel,
          rating,
          demProb,
        });
      } else {
        onDistrictSelect(null);
      }
    }
  };

  return (
    <div className="us-map-container">
      <div className="district-map-wrapper">
        {districtFeatures.length === 0 ? (
          <div style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            height: '400px',
            color: '#666',
            backgroundColor: '#ffffff',
            borderRadius: '8px'
          }}>
            Loading district map...
          </div>
        ) : (
          <svg
            ref={svgCallbackRef}
            viewBox="0 0 800 500"
            style={{
              width: '100%',
              height: 'auto',
              backgroundColor: '#ffffff',
              borderRadius: '8px',
              cursor: isPanning ? 'grabbing' : 'grab',
              userSelect: 'none',
            }}
            onMouseDown={handleMouseDown}
            onMouseMove={handleMouseMove}
            onMouseUp={handleMouseUp}
            onMouseLeave={handleMouseLeave}
          >
            <MapPatternDefs ns="dist" colorOf={getRatingColor} />
            <g transform={`translate(${400 + pan.x}, ${250 + pan.y}) scale(${zoom}) translate(${-400}, ${-250})`}>
              {/* Render non-hovered districts first */}
              {districtFeatures.map((feat) => {
                const stateId = fipsToState[feat.properties.STATEFP];
                const cd = feat.properties.CD118FP;
                const districtNum = cd === '00' ? 1 : parseInt(cd, 10);
                const isHovered = tooltipData?.stateId === stateId && tooltipData?.districtNum === districtNum;

                if (isHovered) return null;

                const race = raceMap.get(`${stateId}-${districtNum}`);
                const rating = race ? (raceRatings?.get(race.id) ?? race.rating) : null;
                const fillColor = !rating
                  ? '#CCCCCC'
                  : patterns
                    ? ratingFill('dist', rating, getRatingColor(rating))
                    : getRatingColor(rating);
                const pathData = paths.get(feat.properties.GEOID) || '';

                return (
                  <path
                    key={feat.properties.GEOID}
                    d={pathData}
                    fill={fillColor}
                    stroke="#FFFFFF"
                    strokeWidth={0.3 / zoom}
                    style={{ cursor: 'pointer' }}
                    onMouseEnter={() => handleDistrictMouseEnter(stateId, districtNum)}
                    onMouseLeave={handleDistrictMouseLeave}
                    onClick={() => handleDistrictClick(stateId, districtNum)}
                  />
                );
              })}
              {/* Render hovered district on top with highlight effect */}
              {districtFeatures.map((feat) => {
                const stateId = fipsToState[feat.properties.STATEFP];
                const cd = feat.properties.CD118FP;
                const districtNum = cd === '00' ? 1 : parseInt(cd, 10);
                const isHovered = tooltipData?.stateId === stateId && tooltipData?.districtNum === districtNum;

                if (!isHovered) return null;

                const race = raceMap.get(`${stateId}-${districtNum}`);
                const rating = race ? (raceRatings?.get(race.id) ?? race.rating) : null;
                const fillColor = !rating
                  ? '#CCCCCC'
                  : patterns
                    ? ratingFill('dist', rating, getRatingColor(rating))
                    : getRatingColor(rating);
                const pathData = paths.get(feat.properties.GEOID) || '';

                return (
                  <path
                    key={`hover-${feat.properties.GEOID}`}
                    d={pathData}
                    fill={fillColor}
                    stroke="#333"
                    strokeWidth={2 / zoom}
                    style={{
                      cursor: 'pointer',
                      filter: 'drop-shadow(0 2px 4px rgba(0,0,0,0.5)) brightness(1.15)',
                    }}
                    onMouseEnter={() => handleDistrictMouseEnter(stateId, districtNum)}
                    onMouseLeave={handleDistrictMouseLeave}
                    onClick={() => handleDistrictClick(stateId, districtNum)}
                  />
                );
              })}
            </g>
          </svg>
        )}

        {/* Instructions */}
        <div className="map-instructions" style={{
          textAlign: 'center',
          fontSize: '12px',
          color: '#888',
          marginTop: '8px',
        }}>
          Scroll to zoom • Drag to pan • Click district to view state
        </div>
      </div>

      {tooltipData && !isPanning && (
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
            {stateNames[tooltipData.stateId] || tooltipData.stateId}
            {tooltipData.districtNum === 1 && !tooltipData.race?.districtNumber
              ? ' - At-Large'
              : ` - District ${tooltipData.districtNum}`}
          </h4>

          {tooltipData.race ? (
            <div>
              <div style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                marginBottom: '8px',
              }}>
                <span style={{ fontWeight: 500 }}>House Race</span>
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
                      <span style={{ color: isDemo ? '#0033AA' : '#AA0000', fontWeight: 500 }}>
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
              No race data available
            </div>
          )}
        </div>
      )}
    </div>
  );
};
