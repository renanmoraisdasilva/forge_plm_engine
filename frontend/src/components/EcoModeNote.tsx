import type { EcoMode } from '../api';

/**
 * Honest environment note while Slice 4–6 are in flight. It is not product chrome:
 * without it the screen would imply data was saved to the database when it was not.
 */
export function EcoModeNote({ mode }: { mode: EcoMode }) {
  if (mode === 'live') return null;

  return (
    <div className="mb-3 flex flex-wrap items-center gap-2 rounded border border-warning-line bg-warning-bg/60 px-3 py-2 text-[11px] text-ink-subtle">
      <span className="rounded-[3px] border border-warning-line bg-surface px-1.5 py-0.5 font-semibold text-warning">
        MOCK ECO API
      </span>
      ECO endpoints are not live yet. This screen uses the in-memory contract adapter; changes
      reset when the page reloads.
    </div>
  );
}
