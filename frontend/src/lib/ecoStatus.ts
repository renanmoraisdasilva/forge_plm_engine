export type EcoAction = 'submit' | 'approve' | 'reject' | 'release';

export type EcoActionMatrix = Record<EcoAction, boolean>;

/**
 * FE-02 status matrix, copied from docs/IMPLEMENTATION.md.
 *
 * This table is the only place the UI decides whether an action is legal. A
 * `409 invalid-transition` reaching the UI means a guard here was missed.
 */
export const ECO_ACTION_MATRIX: Record<string, EcoActionMatrix> = {
  Draft: { submit: true, approve: false, reject: false, release: false },
  Submitted: { submit: false, approve: true, reject: true, release: false },
  Approved: { submit: false, approve: false, reject: false, release: true },
  Rejected: { submit: false, approve: false, reject: false, release: false },
  Applied: { submit: false, approve: false, reject: false, release: false },
};

export function canPerform(status: string, action: EcoAction): boolean {
  return ECO_ACTION_MATRIX[status]?.[action] ?? false;
}

export function actionDisabledReason(status: string, action: EcoAction): string {
  switch (action) {
    case 'submit':
      return 'Only a Draft ECO can be submitted.';
    case 'approve':
      return 'Only a Submitted ECO can be approved.';
    case 'reject':
      return 'Only a Submitted ECO can be rejected.';
    case 'release':
      return 'Only an Approved ECO can be released.';
    default:
      return `This action is not available while the ECO is ${status}.`;
  }
}

/** Release is rendered only for Approved; the contract says so explicitly. */
export function isReleaseVisible(status: string): boolean {
  return status === 'Approved';
}

export function isEcoEditable(status: string): boolean {
  return status === 'Draft';
}
