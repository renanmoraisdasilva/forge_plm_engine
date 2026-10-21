import { isFuture } from './format';

/** Confirmation shown before an ECO with future requested effectivity can be released. */
export function immediateReleaseConfirmation(requestedEffectiveAtUtc: string | null): string | null {
  return isFuture(requestedEffectiveAtUtc)
    ? 'This ECO requested a future effective date. Scheduled effectivity is not supported in this prototype. Release this revision effective immediately?'
    : null;
}
