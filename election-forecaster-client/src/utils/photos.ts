import photoData from '../data/candidatePhotos.json';

export interface CandidatePhoto {
  photo: string; // self-hosted /candidates/*.webp avatar (84px, pre-cropped by the tool)
  page: string;  // the article it came from — used as the attribution link
}

const photos = photoData as Record<string, CandidatePhoto>;

// Keyed by race + exact candidate name so same-named politicians in different
// races can't collide. Regenerate the map with tools/fetch_candidate_photos.py.
export const getCandidatePhoto = (raceId: string, name: string): CandidatePhoto | undefined =>
  photos[`${raceId}|${name}`];

