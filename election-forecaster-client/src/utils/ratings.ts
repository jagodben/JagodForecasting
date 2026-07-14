import { RaceRating } from '../types';

/**
 * Text color for a badge filled with a rating color. The pale "Tilt" fills fail WCAG contrast
 * with white text (~1.9:1), so they get dark ink; the saturated fills keep white.
 */
export const ratingTextColor = (rating: RaceRating | null | undefined): string =>
  rating === RaceRating.TiltDem || rating === RaceRating.TiltRep ? '#1a1a1a' : 'white';
