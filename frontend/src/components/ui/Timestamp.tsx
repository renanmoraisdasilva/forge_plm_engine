import { formatLocalDateTime, formatUtc } from '../../lib/format';

/**
 * Displays local time, carries the stored UTC instant in `title` (A-15).
 * The same component is used everywhere so tables and detail panes cannot drift.
 */
export function Timestamp({ iso, className = '' }: { iso: string | null | undefined; className?: string }) {
  if (!iso) {
    return <span className={`text-ink-muted ${className}`}>{'\u2014'}</span>;
  }
  return (
    <time dateTime={iso} title={formatUtc(iso)} className={className}>
      {formatLocalDateTime(iso)}
    </time>
  );
}
