import { useEffect } from 'react';

const BASE = 'Jagod Forecasting';

// Sets document.title while a page is mounted and restores the base title on unmount. Helps browser
// tabs, history, and JS-capable crawlers; note that static OG scrapers (Slack/Facebook/X) read the
// server HTML, so per-page social cards would need prerendering/SSR — the static tags cover those.
export const useDocumentTitle = (title: string | null | undefined) => {
  useEffect(() => {
    document.title = title ? `${title} — ${BASE}` : BASE;
    return () => {
      document.title = `2026 Election Forecast — ${BASE}`;
    };
  }, [title]);
};
