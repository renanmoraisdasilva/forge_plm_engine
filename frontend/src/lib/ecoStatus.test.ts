import { describe, expect, it } from 'vitest';
import {
  ECO_ACTION_MATRIX,
  actionDisabledReason,
  canPerform,
  isEcoEditable,
  isReleaseVisible,
} from './ecoStatus';

const STATUSES = ['Draft', 'Submitted', 'Approved', 'Rejected', 'Applied'] as const;

describe('ECO status matrix (FE-02)', () => {
  it('matches the frozen contract exactly', () => {
    expect(ECO_ACTION_MATRIX).toEqual({
      Draft: { submit: true, approve: false, reject: false, release: false },
      Submitted: { submit: false, approve: true, reject: true, release: false },
      Approved: { submit: false, approve: false, reject: false, release: true },
      Rejected: { submit: false, approve: false, reject: false, release: false },
      Applied: { submit: false, approve: false, reject: false, release: false },
    });
  });

  it.each(STATUSES)('never allows release before Approved (%s)', (status) => {
    expect(canPerform(status, 'release')).toBe(status === 'Approved');
  });

  it('only exposes Release for an Approved ECO', () => {
    for (const status of STATUSES) {
      expect(isReleaseVisible(status)).toBe(status === 'Approved');
    }
  });

  it('only allows editing a Draft', () => {
    for (const status of STATUSES) {
      expect(isEcoEditable(status)).toBe(status === 'Draft');
    }
  });

  it('rejects unknown statuses rather than defaulting to an action', () => {
    expect(canPerform('Cancelled', 'submit')).toBe(false);
    expect(canPerform('Cancelled', 'release')).toBe(false);
  });

  it('gives every action a reason when disabled', () => {
    expect(actionDisabledReason('Draft', 'submit')).toMatch(/Draft/);
    expect(actionDisabledReason('Draft', 'approve')).toMatch(/Submitted/);
    expect(actionDisabledReason('Approved', 'release')).toMatch(/Approved/);
  });
});
