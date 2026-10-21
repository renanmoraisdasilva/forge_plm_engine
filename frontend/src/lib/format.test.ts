import { describe, expect, it } from 'vitest';
import {
  formatLocalDateTime,
  formatUtc,
  fromLocalInputValue,
  isFuture,
  toLocalInputValue,
} from './format';

describe('UTC-stored, local-displayed dates (A-15)', () => {
  it('shows UTC on hover in a stable, unambiguous form', () => {
    expect(formatUtc('2026-10-21T18:45:00Z')).toBe('2026-10-21 18:45 UTC');
  });

  it('round-trips a local datetime input through UTC', () => {
    const local = '2026-10-21T14:30';
    const iso = fromLocalInputValue(local);
    expect(iso).toBeTruthy();
    expect(toLocalInputValue(iso)).toBe(local);
  });

  it('renders the stored instant in the browser locale, not the raw string', () => {
    const text = formatLocalDateTime('2026-10-21T18:45:00Z');
    expect(text).not.toBe('\u2014');
    expect(text).toMatch(/2026/);
  });

  it('returns an em dash for missing values', () => {
    expect(formatLocalDateTime(null)).toBe('\u2014');
    expect(formatUtc(null)).toBeUndefined();
    expect(toLocalInputValue(null)).toBe('');
    expect(fromLocalInputValue('')).toBeNull();
  });

  it('recognises scheduled (future) dates', () => {
    expect(isFuture('2999-01-01T00:00:00Z')).toBe(true);
    expect(isFuture('2000-01-01T00:00:00Z')).toBe(false);
    expect(isFuture(null)).toBe(false);
  });
});
