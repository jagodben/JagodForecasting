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
  // Wrap the photo in its Wikipedia attribution link (skip inside clickable cards).
  link?: boolean;
}

export const CandidateAvatar = ({ photo, name, size, fallback, link = false, ringColor }: Props) => {
  const [failed, setFailed] = useState(false);
  // A hairline white gap keeps the party color from blending into the photo's edge pixels,
  // so the ring reads as the exact same shade as the headline numbers and chart lines.
  const ring = size >= 40 ? 3 : 2.5;
  const ringShadow = ringColor ? `0 0 0 1.5px #fff, 0 0 0 ${1.5 + ring}px ${ringColor}` : undefined;
  if (!photo || failed) return <>{fallback}</>;

  const img = (
    <img
      src={photo.photo}
      alt={name}
      onError={() => setFailed(true)}
      style={{ width: size, height: size, borderRadius: '50%', objectFit: 'cover', display: 'block', backgroundColor: '#f0f0f0', boxShadow: ringShadow }}
    />
  );

  if (!link) return img;
  return (
    <a
      href={photo.page}
      target="_blank"
      rel="noopener noreferrer"
      title={`${name} — photo via Wikipedia`}
      style={{ flexShrink: 0, lineHeight: 0 }}
    >
      {img}
    </a>
  );
};
