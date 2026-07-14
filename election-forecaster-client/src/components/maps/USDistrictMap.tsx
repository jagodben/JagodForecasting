import { useState, useEffect, useRef, useCallback, useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Race, RaceRating, RaceType } from '../../types';
import { forecastApi } from '../../services/api';
import { geoPath, geoAlbersUsa, GeoPermissibleObjects } from 'd3-geo';
import { feature } from 'topojson-client';
import { ratingFill, MapPatternDefs } from './ratingFill';
import { useAccessibility } from '../../context/AccessibilityContext';
import { ratingTextColor } from '../../utils/ratings';
import { districtCode } from '../../utils/districts';

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

const getRatingColor = (rating: RaceRating): string => {
  switch (rating) {
    case RaceRating.SolidDem: return '#123f8f';
    case RaceRating.LikelyDem: return '#2e63bd';
    case RaceRating.LeanDem: return '#5a8fd6';
    case RaceRating.TiltDem: return '#9dbff0';
    case RaceRating.TiltRep: return '#f4aa9b';
    case RaceRating.LeanRep: return '#e2694f';
    case RaceRating.LikelyRep: return '#cf2f1a';
    case RaceRating.SolidRep: return '#9c150b';
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

// Formats a Dem margin (points) as a called result, e.g. +5.3 → "D+5.3", -3 → "R+3", 0 → "EVEN".
const formatMargin = (margin: number): string => {
  const rounded = Math.round(margin * 10) / 10;
  if (rounded === 0) return 'EVEN';
  const abs = Math.abs(rounded);
  const num = Number.isInteger(abs) ? abs.toString() : abs.toFixed(1);
  return rounded > 0 ? `D+${num}` : `R+${num}`;
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
  stateId: string;
  districtNumber: number;
  rating: RaceRating | null;
  demProb: number | null;
  raceId: string | null;
  // Projected result (e.g. "D+5.3" / "R+3"), pre-formatted with its color.
  marginText: string | null;
  marginColor: string;
}

interface USDistrictMapProps {
  races: Race[];
  dataSource?: DataSource;
  // Fires when a district is tapped/hovered, so the parent can show a mobile info panel.
  onDistrictSelect?: (data: SelectedDistrictData | null) => void;
}

interface DistrictFeature {
  type: string;
  properties: {
    STATEFP: string;
    DISTRICT: string;
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
  const navigate = useNavigate();
  const [tooltipData, setTooltipData] = useState<TooltipData | null>(null);
  const [tooltipPosition, setTooltipPosition] = useState({ x: 0, y: 0 });
  const [districtFeatures, setDistrictFeatures] = useState<DistrictFeature[]>([]);
  const [paths, setPaths] = useState<Map<string, string>>(new Map());

  // Zoom and pan state
  const [zoom, setZoom] = useState(1);
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const [isPanning, setIsPanning] = useState(false);
  const [lastMouse, setLastMouse] = useState({ x: 0, y: 0 });
  // Pointer-drag tracking, kept in refs (not state) so it's correct within a single event tick:
  // pointerDownRef marks the button held, downPosRef the press point, draggedRef whether real
  // movement happened. The click that ends a pan reads draggedRef to avoid counting as a
  // district selection — isPanning state is already cleared by mouseup and lags a tick besides.
  const pointerDownRef = useRef(false);
  const downPosRef = useRef({ x: 0, y: 0 });
  const draggedRef = useRef(false);
  // Two-finger pinch state, and whether the last interaction came from touch (so a tap selects the
  // district for the mobile panel instead of navigating the way a desktop click does).
  const pinchRef = useRef<{ startDist: number; startZoom: number } | null>(null);
  const wasTouchRef = useRef(false);
  // The district previewed by the previous tap — tapping it again opens its race page.
  const lastTapRef = useRef<string | null>(null);
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
      wasTouchRef.current = false;
      setIsPanning(true);
      pointerDownRef.current = true;
      draggedRef.current = false;
      downPosRef.current = { x: e.clientX, y: e.clientY };
      setLastMouse({ x: e.clientX, y: e.clientY });
    }
  }, []);

  const handleMouseMove = useCallback((e: React.MouseEvent) => {
    setTooltipPosition({ x: e.clientX, y: e.clientY });

    // A few pixels of movement while the button is held means panning, not a district click.
    // Tracked off refs so it's correct even before the isPanning state update lands.
    if (pointerDownRef.current &&
        Math.abs(e.clientX - downPosRef.current.x) + Math.abs(e.clientY - downPosRef.current.y) > 3) {
      draggedRef.current = true;
    }

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
    pointerDownRef.current = false;
  }, []);

  const handleMouseLeave = useCallback(() => {
    setIsPanning(false);
    pointerDownRef.current = false;
    setTooltipData(null);
  }, []);

  // Zoom toward the map center by a factor, keeping the centered point stable. Used by the on-screen
  // +/- buttons (the reliable way to zoom on mobile, where there's no scroll wheel).
  const zoomBy = useCallback((factor: number) => {
    setZoom(z => {
      const nz = Math.min(Math.max(z * factor, 1), 10);
      const ratio = nz / z;
      setPan(p => {
        const maxPanX = nz > 1 ? 400 * nz * 0.8 : 0;
        const maxPanY = nz > 1 ? 250 * nz * 0.8 : 0;
        return {
          x: Math.max(-maxPanX, Math.min(maxPanX, p.x * ratio)),
          y: Math.max(-maxPanY, Math.min(maxPanY, p.y * ratio)),
        };
      });
      return nz;
    });
  }, []);

  const resetView = useCallback(() => { setZoom(1); setPan({ x: 0, y: 0 }); }, []);

  const touchDistance = (t: React.TouchList) =>
    Math.hypot(t[0].clientX - t[1].clientX, t[0].clientY - t[1].clientY);

  const handleTouchStart = useCallback((e: React.TouchEvent) => {
    wasTouchRef.current = true;
    const t = e.touches;
    if (t.length === 1) {
      pointerDownRef.current = true;
      draggedRef.current = false;
      downPosRef.current = { x: t[0].clientX, y: t[0].clientY };
      setLastMouse({ x: t[0].clientX, y: t[0].clientY });
      setIsPanning(true);
    } else if (t.length === 2) {
      pinchRef.current = { startDist: touchDistance(t), startZoom: zoomRef.current };
      draggedRef.current = true; // a pinch is never a tap-to-select
    }
  }, []);

  const handleTouchMove = useCallback((e: React.TouchEvent) => {
    const t = e.touches;
    if (t.length === 2 && pinchRef.current) {
      const ratio = touchDistance(t) / (pinchRef.current.startDist || 1);
      const nz = Math.min(Math.max(pinchRef.current.startZoom * ratio, 1), 10);
      const maxPanX = nz > 1 ? 400 * nz * 0.8 : 0;
      const maxPanY = nz > 1 ? 250 * nz * 0.8 : 0;
      setZoom(prev => {
        const r = nz / prev;
        setPan(p => ({
          x: Math.max(-maxPanX, Math.min(maxPanX, p.x * r)),
          y: Math.max(-maxPanY, Math.min(maxPanY, p.y * r)),
        }));
        return nz;
      });
    } else if (t.length === 1 && isPanning) {
      const dx = t[0].clientX - lastMouse.x;
      const dy = t[0].clientY - lastMouse.y;
      if (Math.abs(t[0].clientX - downPosRef.current.x) + Math.abs(t[0].clientY - downPosRef.current.y) > 3)
        draggedRef.current = true;
      const svgElement = svgRef.current;
      if (svgElement) {
        const rect = svgElement.getBoundingClientRect();
        const scaleX = 800 / rect.width;
        const scaleY = 500 / rect.height;
        const maxPanX = zoom > 1 ? 400 * zoom * 0.8 : 0;
        const maxPanY = zoom > 1 ? 250 * zoom * 0.8 : 0;
        setPan(p => ({
          x: Math.max(-maxPanX, Math.min(maxPanX, p.x + dx * scaleX)),
          y: Math.max(-maxPanY, Math.min(maxPanY, p.y + dy * scaleY)),
        }));
      }
      setLastMouse({ x: t[0].clientX, y: t[0].clientY });
    }
  }, [isPanning, lastMouse, zoom]);

  const handleTouchEnd = useCallback(() => {
    setIsPanning(false);
    pointerDownRef.current = false;
    pinchRef.current = null;
    // The tap's synthesized click still fires — handleDistrictClick routes it to selection on touch.
  }, []);

  // Sets the desktop tooltip and the parent's mobile info panel for a district.
  const selectDistrict = (stateId: string, districtNum: number) => {
    const race = raceMap.get(`${stateId}-${districtNum}`);
    setTooltipData({ stateId, districtNum, race: race || null });
    if (!onDistrictSelect) return;
    if (!race) { onDistrictSelect(null); return; }
    const detailed = detailedForecasts?.find(f => f.raceId === race.id);
    const demProb = detailed
      ? detailed.demWinProbability
      : (race.forecasts.find(f =>
          race.candidates.find(c => c.id === f.candidateId)?.party !== 'Republican'
        )?.winProbability ?? null);
    const margin = detailed?.expectedDemMargin;
    onDistrictSelect({
      stateId,
      districtNumber: districtNum,
      rating: raceRatings?.get(race.id) ?? race.rating,
      demProb,
      raceId: race.id,
      marginText: margin != null ? formatMargin(margin) : null,
      marginColor: margin == null ? '#666' : margin > 0 ? '#123f8f' : margin < 0 ? '#9c150b' : '#666',
    });
  };

  const handleDistrictMouseEnter = (stateId: string, districtNum: number) => {
    if (pointerDownRef.current) return; // mid pan/pinch — don't hijack the panel
    selectDistrict(stateId, districtNum);
  };

  const handleDistrictMouseLeave = () => {
    if (!isPanning) setTooltipData(null);
  };

  const handleDistrictClick = (stateId: string, districtNum: number) => {
    if (draggedRef.current) return; // ended a pan/pinch, not a tap

    // Mobile: the first tap selects the district (fills the info panel); tapping the SAME district
    // again — or tapping the panel itself — opens the race page. Desktop click: navigate directly.
    const tapKey = `${stateId}-${districtNum}`;
    if (wasTouchRef.current && lastTapRef.current !== tapKey) {
      lastTapRef.current = tapKey;
      selectDistrict(stateId, districtNum);
      return;
    }
    const race = races.find(r =>
      r.stateId.toLowerCase() === stateId.toLowerCase() &&
      r.districtNumber === districtNum
    );
    if (race) navigate(`/race/${race.id}`);
  };

  return (
    <div className="us-map-container">
      <div className="district-map-wrapper" style={{ position: 'relative' }}>
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
          <>
          {/* Zoom controls — the reliable way to zoom on touch, where there's no scroll wheel. */}
          <div className="map-zoom-controls">
            <button type="button" aria-label="Zoom in" onClick={() => zoomBy(1.4)}>+</button>
            <button type="button" aria-label="Zoom out" onClick={() => zoomBy(1 / 1.4)}>−</button>
            <button type="button" aria-label="Reset zoom" onClick={resetView}>⟲</button>
          </div>
          <svg
            ref={svgCallbackRef}
            viewBox="0 0 800 500"
            role="img"
            aria-label="Map of all 435 House districts colored by forecast rating. For keyboard browsing, open a state page and use its district list and map."
            style={{
              width: '100%',
              height: 'auto',
              backgroundColor: '#ffffff',
              borderRadius: '8px',
              cursor: isPanning ? 'grabbing' : 'grab',
              userSelect: 'none',
              touchAction: 'none', // we handle pan/pinch ourselves; stop the page from scrolling/zooming
            }}
            onMouseDown={handleMouseDown}
            onMouseMove={handleMouseMove}
            onMouseUp={handleMouseUp}
            onMouseLeave={handleMouseLeave}
            onTouchStart={handleTouchStart}
            onTouchMove={handleTouchMove}
            onTouchEnd={handleTouchEnd}
          >
            <MapPatternDefs ns="dist" colorOf={getRatingColor} />
            <g transform={`translate(${400 + pan.x}, ${250 + pan.y}) scale(${zoom}) translate(${-400}, ${-250})`}>
              {/* Base layer: every district stays mounted (even the hovered one) so its
                  mouseenter/mouseleave always fire. The highlighted copy is drawn by the
                  non-interactive overlay layer below — unmounting the hovered node used to drop
                  the mouseleave and leave a district stuck highlighted. */}
              {districtFeatures.map((feat) => {
                const stateId = fipsToState[feat.properties.STATEFP];
                const cd = feat.properties.DISTRICT;
                const districtNum = cd === '00' ? 1 : parseInt(cd, 10);

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
                const cd = feat.properties.DISTRICT;
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
                      // Decoration only — pointerEvents: none keeps the base layer underneath in
                      // charge of hover/click so events are never lost to this transient node.
                      pointerEvents: 'none',
                      filter: 'drop-shadow(0 2px 4px rgba(0,0,0,0.5)) brightness(1.15)',
                    }}
                  />
                );
              })}
            </g>
          </svg>
          </>
        )}

        {/* Instructions */}
        <div className="map-instructions" style={{
          textAlign: 'center',
          fontSize: '12px',
          color: '#6b6b6b',
          marginTop: '8px',
        }}>
          Scroll or use +/− to zoom • Drag to pan • Click a district for details
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
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: '12px', margin: '0 0 8px 0', borderBottom: '1px solid #eee', paddingBottom: '8px' }}>
            <h4 style={{ margin: 0, fontSize: '14px' }}>
              {districtCode(tooltipData.stateId, tooltipData.districtNum)}
            </h4>
            {(() => {
              // Projected result (D+/R+) for the hovered district, from the blended forecast's margin.
              const margin = detailedForecasts?.find(f => f.raceId === tooltipData.race?.id)?.expectedDemMargin;
              if (margin == null) return null;
              return (
                <span style={{ fontSize: '14px', fontWeight: 700, whiteSpace: 'nowrap', color: margin > 0 ? '#123f8f' : margin < 0 ? '#9c150b' : '#666' }}>
                  {formatMargin(margin)}
                </span>
              );
            })()}
          </div>

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
                  color: ratingTextColor(tooltipData.race.rating),
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
                      <span style={{ color: isDemo ? '#123f8f' : '#9c150b', fontWeight: 500 }}>
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
