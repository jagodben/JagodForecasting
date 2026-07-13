// Unresolved nominees are served as "TBD Democrat" / "TBD Republican". They render with a
// trailing asterisk pointing at a shared "primary not decided yet" footnote.
export const isTbdCandidate = (name?: string | null): boolean => !!name && name.startsWith('TBD ');

export const TBD_NOTE = '* Primary not yet decided';
