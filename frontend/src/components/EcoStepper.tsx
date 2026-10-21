import { Fragment } from 'react';
import type { EcoStatus } from '../api/types';

type StepState = 'done' | 'current' | 'future' | 'rejected';

/**
 * SPEC §9.D / D-04a: Draft → Submitted → Approved (or Rejected) → Applied.
 * "Applied" is reached only by the separate release action, never by approval.
 */
export function EcoStepper({ status }: { status: EcoStatus }) {
  const decisionLabel = status === 'Rejected' ? 'Rejected' : 'Approved';

  let states: StepState[];
  switch (status) {
    case 'Draft':
      states = ['current', 'future', 'future', 'future'];
      break;
    case 'Submitted':
      states = ['done', 'current', 'future', 'future'];
      break;
    case 'Approved':
      states = ['done', 'done', 'current', 'future'];
      break;
    case 'Rejected':
      states = ['done', 'done', 'rejected', 'future'];
      break;
    case 'Applied':
      states = ['done', 'done', 'done', 'done'];
      break;
    default:
      states = ['current', 'future', 'future', 'future'];
  }

  const steps = [
    { label: 'Draft', state: states[0] },
    { label: 'Submitted', state: states[1] },
    { label: decisionLabel, state: states[2] },
    { label: 'Applied', state: states[3] },
  ];

  return (
    <div className="flex flex-wrap items-center gap-1" aria-label={`ECO workflow: ${status}`}>
      {steps.map((step, index) => (
        <Fragment key={step.label}>
          {index > 0 ? <span className={`h-px w-4 ${step.state === 'future' ? 'bg-line' : 'bg-line-strong'}`} /> : null}
          <span
            aria-current={step.state === 'current' ? 'step' : undefined}
            className={`inline-flex h-6 items-center gap-1 rounded px-2 text-[11px] font-semibold ${
              step.state === 'current'
                ? 'bg-primary text-white'
                : step.state === 'done'
                  ? 'border border-success-line bg-success-bg text-success-text'
                  : step.state === 'rejected'
                    ? 'border border-danger-line bg-danger-bg text-danger-text'
                    : 'border border-line bg-muted text-ink-muted'
            }`}
          >
            {step.state === 'done' ? <span aria-hidden="true">{'\u2713'}</span> : null}
            {step.label}
          </span>
        </Fragment>
      ))}
    </div>
  );
}
