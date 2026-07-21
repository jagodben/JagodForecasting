import { useState, ReactNode } from 'react';
import { CandidatePhoto } from '../utils/photos';
import { Party } from '../types';

// Ring shades are the party-LOGO colors (sampled from democrat.png / republican.png) — a
// deliberate exception to the site palette, used only on these circles.
const RING_COLORS: Partial<Record<Party, string>> = {
  [Party.Democrat]: '#0044c9',
  [Party.Republican]: '#e81b23',
  [Party.Independent]: '#eab308',
  [Party.Libertarian]: '#FED105',
  [Party.Green]: '#17AA5C',
};

interface Props {
  photo: CandidatePhoto | undefined;
  name: string;
  size: number;
  // Party ring around the photo (logo blue/red, gold for independents).
  ringParty?: Party;
  // Rendered when there is no photo (or it fails to load): the existing party logo/letter.
  fallback: ReactNode;
}

export const CandidateAvatar = ({ photo, name, size, fallback, ringParty }: Props) => {
  const [failed, setFailed] = useState(false);
  const ring = size >= 40 ? 2.5 : 2;
  const ringColor = ringParty ? RING_COLORS[ringParty] ?? '#808080' : undefined;
  const ringShadow = ringColor ? `0 0 0 ${ring}px ${ringColor}` : undefined;
  if (!photo || failed) return <>{fallback}</>;

  // Photos aren't links — sourcing is credited on the About page, and each image's
  // origin article is recorded in candidatePhotos.json.
  return (
    <img
      src={photo.photo}
      alt={name}
      onError={() => setFailed(true)}
      style={{ width: size, height: size, borderRadius: '50%', objectFit: 'cover', display: 'block', backgroundColor: '#f0f0f0', boxShadow: ringShadow }}
    />
  );
};
