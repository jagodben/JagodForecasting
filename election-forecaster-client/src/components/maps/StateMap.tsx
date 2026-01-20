import { useState } from 'react';
import { District, RaceRating } from '../../types';

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

export const StateMap = ({ stateId, districts, onDistrictClick }: StateMapProps) => {
  const [hoveredDistrict, setHoveredDistrict] = useState<District | null>(null);

  // For states with only 1 district (at-large), show a single box
  if (districts.length <= 1) {
    const district = districts[0];
    return (
      <div className="state-map-container">
        <h3>{stateId} Congressional District</h3>
        <div className="at-large-district">
          <div
            className="district-box"
            style={{
              backgroundColor: district ? getRatingColor(district.rating) : '#CCCCCC',
              width: '200px',
              height: '150px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              borderRadius: '8px',
              cursor: 'pointer',
              color: 'white',
              fontWeight: 'bold',
              fontSize: '18px',
            }}
            onClick={() => district && onDistrictClick?.(district)}
          >
            At-Large
          </div>
        </div>
        {district && (
          <DistrictTooltip district={district} />
        )}
      </div>
    );
  }

  // For states with multiple districts, show a grid
  const cols = Math.ceil(Math.sqrt(districts.length));

  return (
    <div className="state-map-container">
      <h3>{stateId} Congressional Districts</h3>
      <div
        className="district-grid"
        style={{
          display: 'grid',
          gridTemplateColumns: `repeat(${cols}, 60px)`,
          gap: '4px',
          justifyContent: 'center',
        }}
      >
        {districts.map((district) => (
          <div
            key={district.id}
            className="district-cell"
            style={{
              backgroundColor: getRatingColor(district.rating),
              width: '60px',
              height: '60px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              borderRadius: '4px',
              cursor: 'pointer',
              color: 'white',
              fontWeight: 'bold',
              fontSize: '14px',
              transition: 'transform 0.2s',
              transform: hoveredDistrict?.id === district.id ? 'scale(1.1)' : 'scale(1)',
            }}
            onMouseEnter={() => setHoveredDistrict(district)}
            onMouseLeave={() => setHoveredDistrict(null)}
            onClick={() => onDistrictClick?.(district)}
          >
            {district.number}
          </div>
        ))}
      </div>
      {hoveredDistrict && (
        <DistrictTooltip district={hoveredDistrict} />
      )}
    </div>
  );
};

interface DistrictTooltipProps {
  district: District;
}

const DistrictTooltip = ({ district }: DistrictTooltipProps) => {
  const race = district.houseRace;
  if (!race) return null;

  const demForecast = race.forecasts.find(f =>
    race.candidates.find(c => c.id === f.candidateId)?.party === 'Democrat'
  );
  const repForecast = race.forecasts.find(f =>
    race.candidates.find(c => c.id === f.candidateId)?.party === 'Republican'
  );

  return (
    <div className="district-tooltip" style={{
      marginTop: '16px',
      padding: '16px',
      backgroundColor: '#f5f5f5',
      borderRadius: '8px',
      maxWidth: '300px',
    }}>
      <h4 style={{ margin: '0 0 8px 0' }}>District {district.number}</h4>
      <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
        {demForecast && (
          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <span style={{ color: '#0015BC' }}>{demForecast.candidateName} (D)</span>
            <span>{(demForecast.winProbability * 100).toFixed(0)}%</span>
          </div>
        )}
        {repForecast && (
          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <span style={{ color: '#BC0000' }}>{repForecast.candidateName} (R)</span>
            <span>{(repForecast.winProbability * 100).toFixed(0)}%</span>
          </div>
        )}
      </div>
    </div>
  );
};
