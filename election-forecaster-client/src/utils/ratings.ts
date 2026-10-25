import { RaceRating } from '../types';

// A race's rating from its projected Dem margin in points: Tilt under 1, Lean 1–4, Likely 4–8,
// Solid 8+. Rated on the margin as displayed (rounded to 0.1, a rounded zero going to the true
// side), so a race shown as "D+1" is Lean, never Tilt. Mirrors the API's
// ForecastOverlay.RatingFromMargin, which sets race.rating.
export const ratingFromMargin = (margin: number): RaceRating => {
  const rounded = Math.round(margin * 10) / 10;
  const demLeads = rounded !== 0 ? rounded > 0 : margin >= 0;
  const abs = Math.abs(rounded);
  if (abs < 1) return demLeads ? RaceRating.TiltDem : RaceRating.TiltRep;
  if (abs < 4) return demLeads ? RaceRating.LeanDem : RaceRating.LeanRep;
  if (abs < 8) return demLeads ? RaceRating.LikelyDem : RaceRating.LikelyRep;
  return demLeads ? RaceRating.SolidDem : RaceRating.SolidRep;
};
