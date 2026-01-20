import { RaceRating } from '../../types';

const legendItems: { rating: RaceRating; label: string; color: string }[] = [
  { rating: RaceRating.SolidDem, label: 'Solid Dem', color: '#0015BC' },
  { rating: RaceRating.LikelyDem, label: 'Likely Dem', color: '#3355DD' },
  { rating: RaceRating.LeanDem, label: 'Lean Dem', color: '#7799EE' },
  { rating: RaceRating.Tossup, label: 'Tossup', color: '#9966CC' },
  { rating: RaceRating.LeanRep, label: 'Lean Rep', color: '#EE7777' },
  { rating: RaceRating.LikelyRep, label: 'Likely Rep', color: '#DD3333' },
  { rating: RaceRating.SolidRep, label: 'Solid Rep', color: '#BC0000' },
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
