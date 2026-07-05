import { createContext, useContext, useEffect, useState } from 'react';
import type { ReactNode } from 'react';

interface AccessibilityContextValue {
  // When true, maps overlay a diagonal hatch on Republican areas so party is distinguishable
  // without relying on color alone. Off by default; persisted across sessions.
  patterns: boolean;
  toggle: () => void;
}

const AccessibilityContext = createContext<AccessibilityContextValue>({
  patterns: false,
  toggle: () => {},
});

const STORAGE_KEY = 'ef-accessibility-patterns';

export const AccessibilityProvider = ({ children }: { children: ReactNode }) => {
  const [patterns, setPatterns] = useState<boolean>(() => {
    try {
      return localStorage.getItem(STORAGE_KEY) === '1';
    } catch {
      return false;
    }
  });

  useEffect(() => {
    try {
      localStorage.setItem(STORAGE_KEY, patterns ? '1' : '0');
    } catch {
      /* ignore storage failures (private mode, etc.) */
    }
  }, [patterns]);

  return (
    <AccessibilityContext.Provider value={{ patterns, toggle: () => setPatterns((p) => !p) }}>
      {children}
    </AccessibilityContext.Provider>
  );
};

export const useAccessibility = () => useContext(AccessibilityContext);
