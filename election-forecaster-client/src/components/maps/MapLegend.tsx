// Color key for the forecast maps — without it the 8 rating shades (plus gray/gold) are
// unexplained until the user hovers something.
const ENTRIES: { label: string; color: string }[] = [
  { label: 'Solid D', color: '#123f8f' },
  { label: 'Likely D', color: '#2e63bd' },
  { label: 'Lean D', color: '#5a8fd6' },
  { label: 'Tilt D', color: '#9dbff0' },
  { label: 'Tilt R', color: '#f4aa9b' },
  { label: 'Lean R', color: '#e2694f' },
  { label: 'Likely R', color: '#cf2f1a' },
  { label: 'Solid R', color: '#9c150b' },
  { label: 'Ind. favored', color: '#b8860b' },
  { label: 'No 2026 race', color: '#DDDDDD' },
];

export const MapLegend = ({ showNoRace = true }: { showNoRace?: boolean }) => (
  <div className="map-color-legend">
    {ENTRIES.filter(e => showNoRace || e.label !== 'No 2026 race').map(e => (
      <span key={e.label} className="map-color-legend__item">
        <span className="map-color-legend__swatch" style={{ backgroundColor: e.color }} />
        {e.label}
      </span>
    ))}
  </div>
);
