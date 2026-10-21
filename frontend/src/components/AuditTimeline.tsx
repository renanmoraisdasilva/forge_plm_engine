import type { AuditEvent } from '../api/types';
import { EmptyBlock, ErrorBlock, LoadingBlock } from './ui/States';
import { Timestamp } from './ui/Timestamp';

const ACTION_LABELS: Record<string, string> = {
  EcoCreated: 'ECO created',
  EcoSubmitted: 'Submitted for review',
  EcoApproved: 'Approved \u2014 revision created',
  EcoRejected: 'Rejected',
  RevisionReleased: 'Revision released',
  ProductCreated: 'Product created',
  ComponentCreated: 'Component created',
};

function detailText(event: AuditEvent): string | null {
  const details = event.details ?? {};
  const revisionCode = typeof details.revisionCode === 'string' ? details.revisionCode : null;
  const superseded = typeof details.supersededRevisionCode === 'string' ? details.supersededRevisionCode : null;
  const reason = typeof details.rejectionReason === 'string' ? details.rejectionReason : null;
  const lineCount = typeof details.lineCount === 'number' ? details.lineCount : null;

  switch (event.action) {
    case 'EcoApproved':
      return revisionCode
        ? `Revision ${revisionCode} created in Approved status. It is not released.`
        : null;
    case 'RevisionReleased':
      return revisionCode
        ? `Revision ${revisionCode} released${superseded ? `; revision ${superseded} superseded` : ''}.`
        : null;
    case 'EcoRejected':
      return reason ? `Reason: ${reason}` : null;
    case 'EcoCreated':
      return lineCount !== null ? `${lineCount} change ${lineCount === 1 ? 'line' : 'lines'} proposed.` : null;
    default:
      return null;
  }
}

export function AuditTimeline({
  events,
  loading = false,
  error,
  onRetry,
}: {
  events: AuditEvent[];
  loading?: boolean;
  error?: unknown;
  onRetry?: () => void;
}) {
  if (loading) return <LoadingBlock label="Loading audit trail" />;
  if (error) return <ErrorBlock error={error} onRetry={onRetry} />;
  if (events.length === 0) {
    return <EmptyBlock title="No audit events yet" description="Actions recorded for this ECO will appear here." />;
  }

  return (
    <ol className="space-y-0 px-3 py-2">
      {events.map((event, index) => {
        const detail = detailText(event);
        return (
          <li key={event.id} className="relative flex gap-3 pb-4 last:pb-1">
            {index < events.length - 1 ? (
              <span className="absolute left-[5px] top-3 h-full w-px bg-line" aria-hidden="true" />
            ) : null}
            <span
              className={`relative z-10 mt-1.5 h-[11px] w-[11px] shrink-0 rounded-full border-2 border-surface ${
                event.action === 'EcoRejected'
                  ? 'bg-danger'
                  : event.action === 'RevisionReleased' || event.action === 'EcoApproved'
                    ? 'bg-success'
                    : 'bg-primary'
              }`}
              aria-hidden="true"
            />
            <div className="min-w-0">
              <div className="flex flex-wrap items-baseline gap-x-2">
                <span className="text-[12px] font-semibold text-ink">
                  {ACTION_LABELS[event.action] ?? event.action}
                </span>
                <span className="text-[11px] text-ink-muted">by {event.actor}</span>
              </div>
              <div className="text-[11px] text-ink-muted">
                <Timestamp iso={event.occurredAtUtc} />
              </div>
              {detail ? <p className="mt-0.5 text-[11px] text-ink-subtle">{detail}</p> : null}
            </div>
          </li>
        );
      })}
    </ol>
  );
}
