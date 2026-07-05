// Compact "time ago" formatting for data-freshness labels (e.g. "just now", "5 min ago",
// "3 hr ago", "2 days ago"). Returns null for missing/invalid input so callers can hide the label.
export const timeAgo = (iso: string | null | undefined): string | null => {
  if (!iso) return null;
  const then = new Date(iso).getTime();
  if (Number.isNaN(then)) return null;

  const seconds = Math.max(0, (Date.now() - then) / 1000);
  if (seconds < 60) return 'just now';

  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes} min ago`;

  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} hr ago`;

  const days = Math.floor(hours / 24);
  if (days < 30) return `${days} day${days === 1 ? '' : 's'} ago`;

  const months = Math.floor(days / 30);
  return `${months} mo ago`;
};
