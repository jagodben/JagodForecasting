import { RaceRating } from '../../types';

// Republican ratings render with a diagonal hatch layered over the base color, so party lean is
// distinguishable without relying on hue alone (colorblind-safe / works in print & forced-colors).
// Democratic ratings stay solid. The shade still carries strength (Tilt → Solid).
const REP_RATINGS: RaceRating[] = [
  RaceRating.TiltRep,
  RaceRating.LeanRep,
  RaceRating.LikelyRep,
  RaceRating.SolidRep,
];

export const isRepRating = (r: RaceRating): boolean => REP_RATINGS.includes(r);

// Pattern ids are namespaced per map so the two maps (which use slightly different color ramps)
// never collide even if both were ever mounted at once.
const patternId = (ns: string, r: RaceRating) => `hatch-${ns}-${r}`;

/**
 * Fill for a geography: the solid rating color for Democratic ratings, or a hatch-pattern reference
 * for Republican ones. Pair with <MapPatternDefs> rendered inside the same <svg>.
 */
export const ratingFill = (ns: string, rating: RaceRating, color: string): string =>
  isRepRating(rating) ? `url(#${patternId(ns, rating)})` : color;

/**
 * SVG <defs> providing one hatch pattern per Republican rating (base color + dark diagonal lines).
 * `colorOf` is the map's own rating→color function so the hatch base matches the solid fills exactly.
 */
export const MapPatternDefs = ({ ns, colorOf }: { ns: string; colorOf: (r: RaceRating) => string }) => (
  <defs>
    {REP_RATINGS.map((r) => (
      <pattern
        key={r}
        id={patternId(ns, r)}
        patternUnits="userSpaceOnUse"
        width="7"
        height="7"
        patternTransform="rotate(45)"
      >
        <rect width="7" height="7" fill={colorOf(r)} />
        <line x1="0" y1="0" x2="0" y2="7" stroke="rgba(0,0,0,0.30)" strokeWidth="2.2" />
      </pattern>
    ))}
  </defs>
);
