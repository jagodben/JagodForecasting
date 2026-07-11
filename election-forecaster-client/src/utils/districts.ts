// States with a single, at-large U.S. House district. Their one district is labeled
// "AL" (e.g. "AK-AL") rather than a district number.
const AT_LARGE_STATES = new Set(['AK', 'DE', 'ND', 'SD', 'VT', 'WY']);

export const isAtLargeState = (stateId: string): boolean => AT_LARGE_STATES.has(stateId);

/** The district's short label: "AL" for at-large states, otherwise the number. */
export const districtLabel = (stateId: string, districtNumber?: number): string =>
  isAtLargeState(stateId) ? 'AL' : String(districtNumber ?? '');

/** The compact district code, e.g. "NY-26" or "AK-AL". */
export const districtCode = (stateId: string, districtNumber?: number): string =>
  `${stateId}-${districtLabel(stateId, districtNumber)}`;
