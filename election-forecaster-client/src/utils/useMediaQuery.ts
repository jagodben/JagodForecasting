import { useEffect, useState } from 'react';

// Reactive CSS media-query hook. Used to branch the race page between the mobile (toggle-driven)
// and desktop (everything-at-once) layouts.
export const useMediaQuery = (query: string): boolean => {
  const [matches, setMatches] = useState<boolean>(() =>
    typeof window !== 'undefined' ? window.matchMedia(query).matches : false
  );

  useEffect(() => {
    const mql = window.matchMedia(query);
    const handler = () => setMatches(mql.matches);
    handler();
    mql.addEventListener('change', handler);
    return () => mql.removeEventListener('change', handler);
  }, [query]);

  return matches;
};

export const useIsDesktop = () => useMediaQuery('(min-width: 900px)');
