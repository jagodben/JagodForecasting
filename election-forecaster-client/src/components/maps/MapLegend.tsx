import { RaceRating } from '../../types';

const legendItems: { rating: RaceRating; label: string; color: string }[] = [
  { rating: RaceRating.SolidDem, label: 'Solid Dem', color: '#0044CC' },
  { rating: RaceRating.LikelyDem, label: 'Likely Dem', color: '#2266DD' },
  { rating: RaceRating.LeanDem, label: 'Lean Dem', color: '#5599EE' },
  { rating: RaceRating.TiltDem, label: 'Tilt Dem', color: '#99CCFF' },
  { rating: RaceRating.TiltRep, label: 'Tilt Rep', color: '#FFCC99' },
  { rating: RaceRating.LeanRep, label: 'Lean Rep', color: '#EE8855' },
  { rating: RaceRating.LikelyRep, label: 'Likely Rep', color: '#DD4422' },
  { rating: RaceRating.SolidRep, label: 'Solid Rep', color: '#CC0000' },
];

export const MapLegend = () => {
  return (
    <div className="map-legend">
      <h4>Race Ratings</h4>
      <div className="legend-items">
        {legendItems.map((item) => (
          <div key={item.rating} className="legend-item">
            <div
              className="legend-color"
              style={{ backgroundColor: item.color }}
            />
            <span>{item.label}</span>
          </div>
        ))}
      </div>
    </div>
  );
};
