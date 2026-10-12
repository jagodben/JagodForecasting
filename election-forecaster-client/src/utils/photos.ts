import photoPaths from '../data/candidatePhotos.json';

const paths = photoPaths as Record<string, string>;

// The candidate's self-hosted avatar (an 84px webp the photo tool pre-crops), keyed by race +
// exact candidate name so same-named politicians in different races can't collide. Regenerate
// with tools/fetch_candidate_photos.py, which keeps each avatar's source in its own manifest —
// this map ships in the JS bundle, so it holds the paths and nothing else.
export const getCandidatePhoto = (raceId: string, name: string): string | undefined =>
  paths[`${raceId}|${name}`];
