import photoData from '../data/candidatePhotos.json';

export interface CandidatePhoto {
  photo: string; // Wikipedia lead-image thumbnail (hotlinked from Wikimedia)
  page: string;  // the article it came from — used as the attribution link
}

const photos = photoData as Record<string, CandidatePhoto>;

// Keyed by race + exact candidate name so same-named politicians in different
// races can't collide. Regenerate the map with tools/fetch_candidate_photos.py.
export const getCandidatePhoto = (raceId: string, name: string): CandidatePhoto | undefined =>
  photos[`${raceId}|${name}`];
