import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import type { BomLine, BomRevision } from '../api/types';
import { listAllRevisionLines } from '../api/productsApi';
import { AuditTimeline } from '../components/AuditTimeline';
import { BomDiff } from '../components/BomDiff';
import { EcoModeNote } from '../components/EcoModeNote';
import { EcoStepper } from '../components/EcoStepper';
import { Button } from '../components/ui/Button';
import { ConfirmDialog } from '../components/ui/ConfirmDialog';
import { PageHeader } from '../components/ui/PageHeader';
import { ErrorBlock, LoadingBlock } from '../components/ui/States';
import { StatusChip } from '../components/ui/StatusChip';
import { Timestamp } from '../components/ui/Timestamp';
import { actionDisabledReason, canPerform, isEcoEditable, isReleaseVisible } from '../lib/ecoStatus';
import { mapEcoError } from '../lib/ecoErrors';
import { fromLocalInputValue, isFuture, toLocalInputValue } from '../lib/format';
import { useAsync } from '../lib/useAsync';
import { useActor } from '../state/ActorProvider';
import { useApi } from '../state/ApiProvider';

type DialogKind = 'approve' | 'reject' | 'release';

interface Notice {
  tone: 'success' | 'danger';
  text: string;
  code?: string;
}

/** Screen D. Actions are gated by the contract's status matrix before they are sent. */
export function EcoDetail() {
  const { ecoId = '' } = useParams();
  const navigate = useNavigate();
  const { products, eco, ecoMode } = useApi();
  const { actor } = useActor();

  const [dialog, setDialog] = useState<DialogKind | null>(null);
  const [comment, setComment] = useState('');
  const [rejectionReason, setRejectionReason] = useState('');
  const [effectiveLocal, setEffectiveLocal] = useState('');
  const [pending, setPending] = useState(false);
  const [submitPending, setSubmitPending] = useState(false);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [notice, setNotice] = useState<Notice | null>(null);

  const { data, error, loading, reload } = useAsync(async () => {
    const [detail, auditPage] = await Promise.all([
      eco.getEco(ecoId),
      eco.listAudit({ entityType: 'EngineeringChangeOrder', entityId: ecoId, pageSize: 200 }),
    ]);

    let baseLines: BomLine[] = [];
    let baseLinesError = false;
    try {
      baseLines = await listAllRevisionLines(products, detail.baseReleasedRevisionId);
    } catch {
      // The comparison still renders the proposed lines; the note below says so.
      baseLinesError = true;
    }

    let approvedRevision: BomRevision | null = null;
    if (detail.approvedRevisionId) {
      try {
        approvedRevision = await products.getBomRevision(detail.approvedRevisionId);
      } catch {
        // Mock-created revisions are not in PostgreSQL, so this read can 404.
        approvedRevision = null;
      }
    }

    return { detail, audits: auditPage.items, baseLines, baseLinesError, approvedRevision };
  }, [eco, products, ecoId]);

  if (loading && !data) {
    return (
      <div className="rounded border border-line bg-surface">
        <LoadingBlock label="Loading engineering change" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="rounded border border-line bg-surface">
        <ErrorBlock error={error} onRetry={reload} />
      </div>
    );
  }

  if (!data) return null;

  const { detail, audits, baseLines, baseLinesError, approvedRevision } = data;
  const status = detail.status;

  const openDialog = (kind: DialogKind) => {
    setNotice(null);
    setDialogError(null);
    setComment('');
    setRejectionReason('');
    setEffectiveLocal(
      kind === 'release'
        ? toLocalInputValue(detail.requestedEffectiveAtUtc ?? new Date().toISOString())
        : '',
    );
    setDialog(kind);
  };

  const handleSubmit = async () => {
    setNotice(null);
    setSubmitPending(true);
    try {
      await eco.submitEco(detail.id, actor);
      setNotice({ tone: 'success', text: 'ECO submitted for review. It is now waiting for a decision.' });
      reload();
    } catch (reason) {
      const mapped = mapEcoError(reason);
      setNotice({
        tone: 'danger',
        text: mapped.form ?? Object.values(mapped.fields)[0] ?? 'The request failed.',
        code: mapped.code,
      });
    } finally {
      setSubmitPending(false);
    }
  };

  const runDialogAction = async () => {
    setPending(true);
    setDialogError(null);
    try {
      if (dialog === 'approve') {
        await eco.approveEco(detail.id, { actor, comment: comment.trim() || null });
        setNotice({
          tone: 'success',
          text: 'ECO approved. A new BOM revision was created in Approved status. It is not released \u2014 release is a separate action.',
        });
      } else if (dialog === 'reject') {
        await eco.rejectEco(detail.id, { actor, rejectionReason });
        setNotice({ tone: 'success', text: 'ECO rejected. No BOM revision was created.' });
      } else if (dialog === 'release') {
        const effectiveAtUtc = fromLocalInputValue(effectiveLocal);
        if (!effectiveAtUtc) {
          setDialogError('Choose an effective date.');
          setPending(false);
          return;
        }
        await eco.releaseEco(detail.id, { actor, effectiveAtUtc });
        setNotice({
          tone: 'success',
          text: 'ECO applied. The approved revision is now released and the previous revision is superseded.',
        });
      }
      setDialog(null);
      reload();
    } catch (reason) {
      const mapped = mapEcoError(reason);
      setDialogError(mapped.form ?? Object.values(mapped.fields)[0] ?? 'The request failed.');
    } finally {
      setPending(false);
    }
  };

  const dialogTitle =
    dialog === 'approve'
      ? `Approve ${detail.ecoNumber}?`
      : dialog === 'reject'
        ? `Reject ${detail.ecoNumber}?`
        : `Release ${detail.ecoNumber}?`;

  const dialogDescription =
    dialog === 'approve' ? (
      <>
        Approval creates a new BOM revision from revision <span className="mono">{detail.baseRevisionCode}</span> plus
        these changes, in <strong>Approved</strong> status. It does <strong>not</strong> release the revision; release is
        a separate action.
      </>
    ) : dialog === 'reject' ? (
      <>Rejecting closes this ECO. No BOM revision is created, and the reason is recorded in the audit trail.</>
    ) : (
      <>
        Release makes the approved revision the product&apos;s current released configuration, marks the previous
        revision Superseded, and sets this ECO to Applied. Release takes effect immediately; a future date is stored as
        a recorded label and shown as Scheduled.
      </>
    );

  const confirmDisabled =
    pending ||
    (dialog === 'reject' && !rejectionReason.trim()) ||
    (dialog === 'release' && !effectiveLocal);

  return (
    <div>
      <PageHeader
        breadcrumbs={[{ label: 'Engineering Changes', to: '/ecos' }, { label: detail.ecoNumber }]}
        title={detail.title}
        subtitle={
          <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
            <span className="mono text-[13px] font-medium text-ink">{detail.ecoNumber}</span>
            <StatusChip status={status} />
            <Link to={`/products/${detail.productId}`} className="text-primary hover:underline">
              <span className="mono">{detail.product.partNumber}</span>
              <span className="ml-1 text-ink-subtle">{detail.product.name}</span>
            </Link>
            <span className="text-ink-muted">
              Created by {detail.createdBy} <Timestamp iso={detail.createdAtUtc} />
            </span>
          </div>
        }
        actions={
          <>
            {isEcoEditable(status) ? (
              <Button onClick={() => navigate(`/ecos/${detail.id}/edit`)}>Edit draft</Button>
            ) : null}
            <Button
              disabled={!canPerform(status, 'submit') || submitPending}
              title={canPerform(status, 'submit') ? 'Submit this ECO for review' : actionDisabledReason(status, 'submit')}
              onClick={handleSubmit}
            >
              {submitPending ? 'Submitting\u2026' : 'Submit'}
            </Button>
            <Button
              disabled={!canPerform(status, 'approve')}
              title={canPerform(status, 'approve') ? 'Approve this ECO' : actionDisabledReason(status, 'approve')}
              onClick={() => openDialog('approve')}
            >
              Approve
            </Button>
            <Button
              variant="danger"
              disabled={!canPerform(status, 'reject')}
              title={canPerform(status, 'reject') ? 'Reject this ECO' : actionDisabledReason(status, 'reject')}
              onClick={() => openDialog('reject')}
            >
              Reject
            </Button>
            {isReleaseVisible(status) ? (
              <Button variant="primary" onClick={() => openDialog('release')}>
                Release
              </Button>
            ) : null}
          </>
        }
      />

      <EcoModeNote mode={ecoMode} />

      {notice ? (
        <div
          className={`mb-3 flex flex-wrap items-center gap-2 rounded border px-3 py-2 text-[12px] ${
            notice.tone === 'success'
              ? 'border-success-line bg-success-bg/60 text-success-text'
              : 'border-danger-line bg-danger-bg/60 text-danger-text'
          }`}
          role="status"
        >
          <span>{notice.text}</span>
          {notice.code ? (
            <code className="mono rounded border border-danger-line bg-surface px-1.5 py-0.5 text-[11px]">
              {notice.code}
            </code>
          ) : null}
          {notice.code === 'stale-base-revision' ? (
            <Link to={`/ecos/new?productId=${detail.productId}`} className="font-semibold underline">
              Create a new ECO
            </Link>
          ) : null}
        </div>
      ) : null}

      {status === 'Rejected' && detail.reviewerComment ? (
        <div className="mb-3 rounded border border-danger-line bg-danger-bg/60 px-3 py-2 text-[12px] text-danger-text">
          <span className="font-semibold">Rejected by {detail.decidedBy ?? 'reviewer'}:</span> {detail.reviewerComment}
        </div>
      ) : null}

      <div className="mb-4 rounded border border-line bg-surface px-3 py-2">
        <EcoStepper status={status} />
        <p className="mt-1.5 text-[11px] text-ink-muted">
          Approval creates a revision in Approved status. Release is a separate action that makes it current.
        </p>
      </div>

      <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_320px]">
        <div className="space-y-4">
          <section className="overflow-hidden rounded border border-line bg-surface">
            <h2 className="border-b border-line px-3 py-2 text-[13px] font-semibold text-ink">Change request</h2>
            <div className="grid gap-4 px-3 py-3 md:grid-cols-2">
              <div>
                <div className="text-[10px] font-semibold uppercase tracking-[0.06em] text-ink-muted">Reason for change</div>
                <p className="mt-1 whitespace-pre-wrap text-[12px] text-ink">{detail.reasonForChange || '\u2014'}</p>
              </div>
              <div>
                <div className="text-[10px] font-semibold uppercase tracking-[0.06em] text-ink-muted">Impact assessment</div>
                <p className="mt-1 whitespace-pre-wrap text-[12px] text-ink">
                  {detail.impactAssessment || 'Not recorded.'}
                </p>
              </div>
            </div>
            <dl className="grid gap-x-4 gap-y-2 border-t border-line px-3 py-3 text-[12px] md:grid-cols-3">
              <div>
                <dt className="text-[10px] font-semibold uppercase tracking-[0.06em] text-ink-muted">Base revision</dt>
                <dd className="mono mt-0.5 text-ink">{detail.baseRevisionCode}</dd>
              </div>
              <div>
                <dt className="text-[10px] font-semibold uppercase tracking-[0.06em] text-ink-muted">Requested effective</dt>
                <dd className="mt-0.5 text-ink">
                  <Timestamp iso={detail.requestedEffectiveAtUtc} />
                  {isFuture(detail.requestedEffectiveAtUtc) ? (
                    <span className="ml-2 text-[11px] font-semibold text-warning">Scheduled</span>
                  ) : null}
                </dd>
              </div>
              <div>
                <dt className="text-[10px] font-semibold uppercase tracking-[0.06em] text-ink-muted">Change lines</dt>
                <dd className="mt-0.5 tabular-nums text-ink">{detail.lines.length}</dd>
              </div>
            </dl>
          </section>

          <section className="overflow-hidden rounded border border-line bg-surface">
            <div className="flex items-center justify-between border-b border-line px-3 py-2">
              <h2 className="text-[13px] font-semibold text-ink">Before / after BOM comparison</h2>
              <span className="mono text-[11px] text-ink-muted">base rev {detail.baseRevisionCode}</span>
            </div>
            {baseLinesError ? (
              <p className="border-b border-line bg-warning-bg/50 px-3 py-1.5 text-[11px] text-warning">
                The base BOM lines could not be loaded, so only the proposed change lines are shown.
              </p>
            ) : null}
            <BomDiff baseLines={baseLines} changes={detail.lines} />
          </section>

          <section className="overflow-hidden rounded border border-line bg-surface">
            <h2 className="border-b border-line px-3 py-2 text-[13px] font-semibold text-ink">Audit trail</h2>
            <AuditTimeline events={audits} />
          </section>
        </div>

        <aside className="space-y-4">
          <section className="overflow-hidden rounded border border-line bg-surface">
            <h2 className="border-b border-line px-3 py-2 text-[13px] font-semibold text-ink">Workflow</h2>
            <dl className="divide-y divide-line px-3 text-[12px]">
              <div className="py-2">
                <dt className="text-[10px] font-semibold uppercase tracking-[0.06em] text-ink-muted">Created</dt>
                <dd className="mt-0.5 text-ink">
                  {detail.createdBy} <Timestamp iso={detail.createdAtUtc} />
                </dd>
              </div>
              <div className="py-2">
                <dt className="text-[10px] font-semibold uppercase tracking-[0.06em] text-ink-muted">Submitted</dt>
                <dd className="mt-0.5 text-ink">
                  <Timestamp iso={detail.submittedAtUtc} />
                </dd>
              </div>
              <div className="py-2">
                <dt className="text-[10px] font-semibold uppercase tracking-[0.06em] text-ink-muted">Decision</dt>
                <dd className="mt-0.5 text-ink">
                  {detail.decidedBy ? (
                    <>
                      {detail.decidedBy} <Timestamp iso={detail.decidedAtUtc} />
                    </>
                  ) : (
                    <span className="text-ink-muted">Pending</span>
                  )}
                </dd>
              </div>
              <div className="py-2">
                <dt className="text-[10px] font-semibold uppercase tracking-[0.06em] text-ink-muted">Approved revision</dt>
                <dd className="mt-0.5 text-ink">
                  {detail.approvedRevisionId ? (
                    <span className="flex flex-wrap items-center gap-2">
                      <span className="mono font-semibold">
                        {approvedRevision ? approvedRevision.revisionCode : 'Revision created'}
                      </span>
                      <StatusChip
                        status={
                          approvedRevision
                            ? approvedRevision.status
                            : detail.status === 'Applied'
                              ? 'Released'
                              : 'Approved'
                        }
                      />
                    </span>
                  ) : (
                    <span className="text-ink-muted">None</span>
                  )}
                </dd>
              </div>
              <div className="py-2">
                <dt className="text-[10px] font-semibold uppercase tracking-[0.06em] text-ink-muted">Reviewer comment</dt>
                <dd className="mt-0.5 whitespace-pre-wrap text-ink">
                  {detail.reviewerComment || <span className="text-ink-muted">None</span>}
                </dd>
              </div>
            </dl>
          </section>

          <section className="overflow-hidden rounded border border-line bg-surface px-3 py-2">
            <h2 className="text-[10px] font-semibold uppercase tracking-[0.06em] text-ink-muted">Product</h2>
            <Link to={`/products/${detail.productId}`} className="mt-1 block text-[12px] text-primary hover:underline">
              <span className="mono">{detail.product.partNumber}</span>
              <span className="ml-1 text-ink-subtle">{detail.product.name}</span>
            </Link>
          </section>
        </aside>
      </div>

      <ConfirmDialog
        open={dialog !== null}
        title={dialogTitle}
        description={dialogDescription}
        actor={actor}
        confirmLabel={dialog === 'approve' ? 'Approve ECO' : dialog === 'reject' ? 'Reject ECO' : 'Release revision'}
        tone={dialog === 'reject' ? 'danger' : 'primary'}
        pending={pending}
        confirmDisabled={
          (dialog === 'reject' && !rejectionReason.trim()) || (dialog === 'release' && !effectiveLocal)
        }
        error={dialogError}
        onCancel={() => setDialog(null)}
        onConfirm={() => {
          if (!confirmDisabled) void runDialogAction();
        }}
      >
        {dialog === 'approve' ? (
          <label className="block text-[12px] text-ink-subtle">
            Review comment (optional)
            <textarea
              value={comment}
              onChange={(event) => setComment(event.target.value)}
              rows={3}
              className="mt-1 w-full rounded border border-line-strong bg-surface px-2 py-1.5 text-[12px] text-ink focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
            />
          </label>
        ) : null}

        {dialog === 'reject' ? (
          <label className="block text-[12px] text-ink-subtle">
            Rejection reason (required)
            <textarea
              value={rejectionReason}
              onChange={(event) => setRejectionReason(event.target.value)}
              rows={3}
              required
              className="mt-1 w-full rounded border border-line-strong bg-surface px-2 py-1.5 text-[12px] text-ink focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
            />
          </label>
        ) : null}

        {dialog === 'release' ? (
          <label className="block text-[12px] text-ink-subtle">
            Effective date (required)
            <input
              type="datetime-local"
              value={effectiveLocal}
              onChange={(event) => setEffectiveLocal(event.target.value)}
              required
              className="mt-1 h-[30px] w-full rounded border border-line-strong bg-surface px-2 text-[12px] text-ink focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
            />
            <span className="mt-1 block text-[11px] text-ink-muted">
              Stored as UTC and displayed in local time. Release takes effect immediately, even with a future date.
            </span>
          </label>
        ) : null}
      </ConfirmDialog>
    </div>
  );
}
