// Partisan-poll badge, tinted by the sponsor's side: blue for (D), red for (R),
// amber for independents or an unknown direction.
export const PartisanBadge = ({ lean }: { lean?: string }) => {
  const tint =
    lean === 'D' ? { color: '#123f8f', backgroundColor: '#e8eef8' } :
    lean === 'R' ? { color: '#9c150b', backgroundColor: '#faeae8' } :
    { color: '#b45309', backgroundColor: '#fef3c7' };
  return (
    <span style={{ marginLeft: '6px', fontSize: '11px', padding: '1px 6px', borderRadius: '4px', whiteSpace: 'nowrap', ...tint }}>
      partisan
    </span>
  );
};
