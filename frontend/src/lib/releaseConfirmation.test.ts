import { describe, expect, it } from 'vitest';
import { immediateReleaseConfirmation } from './releaseConfirmation';

describe('immediate release confirmation', () => {
  it('explains that a requested future date will be ignored and asks to release now', () => {
    expect(immediateReleaseConfirmation('2999-01-01T00:00:00Z')).toContain(
      'Scheduled effectivity is not supported in this prototype. Release this revision effective immediately?',
    );
  });

  it('does not ask for special confirmation when the requested date is not in the future', () => {
    expect(immediateReleaseConfirmation('2000-01-01T00:00:00Z')).toBeNull();
    expect(immediateReleaseConfirmation(null)).toBeNull();
  });
});
