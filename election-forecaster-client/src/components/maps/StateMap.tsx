import { useState, useMemo, useEffect } from 'react';
import { District, RaceRating } from '../../types';
import { geoPath, geoAlbersUsa } from 'd3-geo';
import { feature } from 'topojson-client';

// Local TopoJSON file with 118th Congress districts
const DISTRICTS_URL = '/data/districts.json';

// State abbreviation to FIPS code mapping
const stateToFips: Record<string, string> = {
  AL: '01', AK: '02', AZ: '04', AR: '05', CA: '06', CO: '08', CT: '09', DE: '10',
  FL: '12', GA: '13', HI: '15', ID: '16', IL: '17', IN: '18', IA: '19', KS: '20',
  KY: '21', LA: '22', ME: '23', MD: '24', MA: '25', MI: '26', MN: '27', MS: '28',
  MO: '29', MT: '30', NE: '31', NV: '32', NH: '33', NJ: '34', NM: '35', NY: '36',
  NC: '37', ND: '38', OH: '39', OK: '40', OR: '41', PA: '42', RI: '44', SC: '45',
  SD: '46', TN: '47', TX: '48', UT: '49', VT: '50', VA: '51', WA: '53', WV: '54',
  WI: '55', WY: '56'
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

interface StateMapProps {
  stateId: string;
  districts: District[];
  onDistrictClick?: (district: District) => void;
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

export const StateMap = ({ stateId, districts, onDistrictClick }: StateMapProps) => {
  const [hoveredDistrict, setHoveredDistrict] = useState<District | null>(null);
  const [tooltipPosition, setTooltipPosition] = useState({ x: 0, y: 0 });
  const [districtFeatures, setDistrictFeatures] = useState<DistrictFeature[]>([]);
  const [paths, setPaths] = useState<Map<string, string>>(new Map());

  const stateFips = stateToFips[stateId] || '';
  const districtMap = useMemo(() =>
    new Map(districts.map(d => [d.number, d])),
    [districts]
  );

  // Load and process TopoJSON data
  useEffect(() => {
    fetch(DISTRICTS_URL)
      .then(res => res.json())
      .then(topology => {
        // Get the first (and only) object name from the topology
        const objectName = Object.keys(topology.objects)[0];
        const geoJson = feature(topology, topology.objects[objectName]) as any;

        // Filter to only this state's districts
        const stateDistricts = geoJson.features.filter((f: DistrictFeature) =>
          f.properties.STATEFP === stateFips
        );

        if (stateDistricts.length === 0) {
          console.warn(`No districts found for state ${stateId} (FIPS: ${stateFips})`);
          return;
        }

        // Create projection and calculate bounds for this state
        const baseProjection = geoAlbersUsa().scale(1000).translate([400, 250]);
        const basePath = geoPath().projection(baseProjection);

        // Calculate bounds for all state districts
        let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
        stateDistricts.forEach((district: DistrictFeature) => {
          const bounds = basePath.bounds(district);
          if (bounds && bounds[0] && bounds[1]) {
            minX = Math.min(minX, bounds[0][0]);
            minY = Math.min(minY, bounds[0][1]);
            maxX = Math.max(maxX, bounds[1][0]);
            maxY = Math.max(maxY, bounds[1][1]);
          }
        });

        // Calculate scale and translation to fit state in view
        const width = maxX - minX;
        const height = maxY - minY;
        const centerX = (minX + maxX) / 2;
        const centerY = (minY + maxY) / 2;

        const svgWidth = 500;
        const svgHeight = 400;
        const padding = 30;

        const scale = Math.min(
          (svgWidth - padding * 2) / width,
          (svgHeight - padding * 2) / height
        );

        // Generate paths with transformed coordinates
        const pathMap = new Map<string, string>();
        stateDistricts.forEach((district: DistrictFeature) => {
          const pathData = basePath(district);
          if (pathData) {
            // Apply transform to center the state
            pathMap.set(district.properties.GEOID, pathData);
          }
        });

        // Store transform info for SVG
        const translateX = svgWidth / 2 - centerX * scale;
        const translateY = svgHeight / 2 - centerY * scale;

        // Regenerate paths with proper scaling
        const scaledProjection = geoAlbersUsa()
          .scale(1000 * scale)
          .translate([
            400 * scale + translateX,
            250 * scale + translateY
          ]);
        const scaledPath = geoPath().projection(scaledProjection);

        const scaledPathMap = new Map<string, string>();
        stateDistricts.forEach((district: DistrictFeature) => {
          const pathData = scaledPath(district);
          if (pathData) {
            scaledPathMap.set(district.properties.GEOID, pathData);
          }
        });

        setDistrictFeatures(stateDistricts);
        setPaths(scaledPathMap);
      })
      .catch(err => console.error('Failed to load district data:', err));
  }, [stateId, stateFips]);

  const handleMouseEnter = (districtNum: number, event: React.MouseEvent) => {
    const district = districtMap.get(districtNum);
    if (district) {
      setHoveredDistrict(district);
      setTooltipPosition({ x: event.clientX, y: event.clientY });
    }
  };

  const handleMouseMove = (event: React.MouseEvent) => {
    setTooltipPosition({ x: event.clientX, y: event.clientY });
  };

  const handleMouseLeave = () => {
    setHoveredDistrict(null);
  };

  const handleClick = (districtNum: number) => {
    const district = districtMap.get(districtNum);
    if (district && onDistrictClick) {
      onDistrictClick(district);
    }
  };

  const isAtLarge = districts.length <= 1;

  return (
    <div className="state-map-container">
      <h3>{stateId} Congressional District{districts.length !== 1 ? 's' : ''}</h3>

      <div style={{ width: '70vw', maxWidth: '900px', minWidth: '400px', margin: '0 auto' }}>
        {districtFeatures.length === 0 ? (
          <div style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            height: '500px',
            color: '#666',
            backgroundColor: '#f8f9fa',
            borderRadius: '8px'
          }}>
            Loading district map...
          </div>
        ) : (
          <svg
            viewBox="0 0 500 400"
            style={{
              width: '100%',
              height: 'auto',
              backgroundColor: '#f8f9fa',
              borderRadius: '8px'
            }}
          >
            {districtFeatures.map((feat) => {
              const cd = feat.properties.CD118FP;
              // Convert district code: "00" means at-large (district 1), otherwise parse as number
              const districtNum = cd === '00' ? 1 : parseInt(cd, 10);

              const district = districtMap.get(isAtLarge ? 1 : districtNum);
              const fillColor = district ? getRatingColor(district.rating) : '#CCCCCC';
              const isHovered = hoveredDistrict?.number === (isAtLarge ? 1 : districtNum);

              const pathData = paths.get(feat.properties.GEOID) || '';

              return (
                <path
                  key={feat.properties.GEOID}
                  d={pathData}
                  fill={isHovered ? '#FFD700' : fillColor}
                  stroke="#FFFFFF"
                  strokeWidth={1.5}
                  style={{ cursor: 'pointer', transition: 'fill 0.15s ease' }}
                  onMouseEnter={(e) => handleMouseEnter(isAtLarge ? 1 : districtNum, e)}
                  onMouseMove={handleMouseMove}
                  onMouseLeave={handleMouseLeave}
                  onClick={() => handleClick(isAtLarge ? 1 : districtNum)}
                />
              );
            })}
          </svg>
        )}
      </div>

      {hoveredDistrict && (
        <div
          style={{
            position: 'fixed',
            left: tooltipPosition.x + 10,
            top: tooltipPosition.y + 10,
            backgroundColor: 'rgba(255, 255, 255, 0.98)',
            border: '1px solid #ccc',
            borderRadius: '8px',
            padding: '12px',
            boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
            pointerEvents: 'none',
            zIndex: 1000,
            minWidth: '220px',
          }}
        >
          <DistrictTooltipContent district={hoveredDistrict} isAtLarge={isAtLarge} />
        </div>
      )}
    </div>
  );
};

interface DistrictTooltipContentProps {
  district: District;
  isAtLarge: boolean;
}

const DistrictTooltipContent = ({ district, isAtLarge }: DistrictTooltipContentProps) => {
  const race = district.houseRace;

  return (
    <div>
      <h4 style={{ margin: '0 0 8px 0', fontSize: '14px', borderBottom: '1px solid #eee', paddingBottom: '8px' }}>
        {isAtLarge ? 'At-Large District' : `District ${district.number}`}
      </h4>
      {race ? (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '6px', fontSize: '13px' }}>
          {race.forecasts.map((forecast) => {
            const candidate = race.candidates.find(c => c.id === forecast.candidateId);
            const isDemo = candidate?.party === 'Democrat';
            return (
              <div key={forecast.candidateId} style={{ display: 'flex', justifyContent: 'space-between', gap: '16px' }}>
                <span style={{ color: isDemo ? '#0015BC' : '#BC0000', fontWeight: 500 }}>
                  {candidate?.party === 'Democrat' ? 'D' : 'R'}: {forecast.candidateName}
                </span>
                <span style={{ fontWeight: 'bold' }}>{(forecast.winProbability * 100).toFixed(0)}%</span>
              </div>
            );
          })}
        </div>
      ) : (
        <div style={{ color: '#666', fontSize: '13px' }}>No race data available</div>
      )}
    </div>
  );
};
