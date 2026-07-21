import { useState, ReactNode } from 'react';
import { CandidatePhoto } from '../utils/photos';

interface Props {
  photo: CandidatePhoto | undefined;
  name: string;
  size: number;
  // Party ring around the photo (blue/red, gold for independents).
  ringColor?: string;
  // Rendered when there is no photo (or it fails to load): the existing party logo/letter.
  fallback: ReactNode;
}

export const CandidateAvatar = ({ photo, name, size, fallback, ringColor }: Props) => {
  const [failed, setFailed] = useState(false);
  const ring = size >= 40 ? 2.5 : 2;
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
