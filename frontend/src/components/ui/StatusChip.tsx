type Tone = 'success' | 'warning' | 'danger' | 'neutral' | 'info';

const TONES: Record<Tone, string> = {
  success: 'border-success-line bg-success-bg text-success-text',
  warning: 'border-warning-line bg-warning-bg text-warning',
  danger: 'border-danger-line bg-danger-bg text-danger-text',
  neutral: 'border-line-strong bg-muted text-ink-subtle',
  info: 'border-info-line bg-info-bg text-info-text',
};

/**
 * Lifecycle vocabulary comes from the domain enums, never from mockup copy.
 * `Applied` is blue rather than green so "approved" and "released/applied" remain
 * visually distinct (D-04a).
 */
const STATUS_TONES: Record<string, Tone> = {
  Active: 'success',
  Released: 'success',
  Approved: 'success',
  Submitted: 'warning',
  Rejected: 'danger',
  Applied: 'info',
  Draft: 'neutral',
  Superseded: 'neutral',
  Retired: 'neutral',
};

export function StatusChip({ status, className = '' }: { status: string; className?: string }) {
  const tone = STATUS_TONES[status] ?? 'neutral';
  return (
    <span
      className={`inline-flex h-5 items-center rounded-[3px] border px-1.5 text-[11px] font-semibold leading-none ${TONES[tone]} ${className}`}
    >
      {status}
    </span>
  );
}
