const localDateTime = new Intl.DateTimeFormat(undefined, {
  year: 'numeric',
  month: 'short',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
});

/**
 * A-15: stored UTC, displayed local, UTC on hover.
 *
 * Local formatting uses the browser's locale; the `title` is derived from the ISO
 * value's UTC parts so a reviewer can always recover the stored instant.
 */
export function formatLocalDateTime(iso: string | null | undefined): string {
  if (!iso) return '\u2014';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  return localDateTime.format(date);
}

export function formatUtc(iso: string | null | undefined): string | undefined {
  if (!iso) return undefined;
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return iso;
  return `${date.toISOString().slice(0, 16).replace('T', ' ')} UTC`;
}

function pad(value: number): string {
  return String(value).padStart(2, '0');
}

/** `YYYY-MM-DDTHH:mm` in the browser's timezone, for `datetime-local` inputs. */
export function toLocalInputValue(iso: string | null | undefined): string {
  if (!iso) return '';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '';
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** Inverse of `toLocalInputValue`: interprets the input locally, returns ISO UTC. */
export function fromLocalInputValue(value: string): string | null {
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  return date.toISOString();
}

export function isFuture(iso: string | null | undefined): boolean {
  if (!iso) return false;
  return Date.parse(iso) > Date.now();
}
