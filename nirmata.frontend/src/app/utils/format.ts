/**
 * Shared formatting utilities.
 */

/**
 * Returns a human-readable relative time string from an ISO timestamp.
 *
 * @example relativeTime("2026-03-09T10:00:00Z") // "2h ago"
 */
export function relativeTime(ts: string): string {
  if (!ts) return "—";
  const diff = Date.now() - new Date(ts).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return "just now";
  if (mins < 60) return `${mins}m ago`;
  const hours = Math.floor(mins / 60);
  if (hours < 24) return `${hours}h ago`;
  return `${Math.floor(hours / 24)}d ago`;
}
