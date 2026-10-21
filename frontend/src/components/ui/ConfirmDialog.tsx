import { useEffect, type ReactNode } from 'react';
import { Button } from './Button';

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  description?: ReactNode;
  actor: string;
  confirmLabel: string;
  tone?: 'primary' | 'danger';
  pending?: boolean;
  confirmDisabled?: boolean;
  error?: string | null;
  onCancel: () => void;
  onConfirm: () => void;
  children?: ReactNode;
}

/**
 * FE-03: Approve, Reject and Release always pass through this dialog and always show
 * the current actor, taken from the header switch at the moment of confirmation.
 */
export function ConfirmDialog({
  open,
  title,
  description,
  actor,
  confirmLabel,
  tone = 'primary',
  pending = false,
  confirmDisabled = false,
  error,
  onCancel,
  onConfirm,
  children,
}: ConfirmDialogProps) {
  useEffect(() => {
    if (!open) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !pending) onCancel();
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [open, pending, onCancel]);

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-start justify-center bg-[rgba(15,23,42,0.45)] p-4 pt-24"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget && !pending) onCancel();
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="confirm-dialog-title"
        className="w-full max-w-[460px] rounded-md border border-[#94a3b8] bg-surface shadow-[0_8px_16px_-2px_rgba(15,23,42,0.12)]"
      >
        <div className="border-b border-line px-4 py-3">
          <h2 id="confirm-dialog-title" className="text-[15px] font-semibold text-ink">
            {title}
          </h2>
        </div>
        <div className="space-y-3 px-4 py-3">
          {description ? <div className="text-[12px] text-ink-subtle">{description}</div> : null}

          <div className="rounded border border-line bg-muted px-2.5 py-2 text-[12px] text-ink-subtle">
            Acting as <span className="font-semibold text-ink">{actor}</span>
          </div>

          {children}

          {error ? (
            <p className="rounded border border-danger-line bg-danger-bg px-2.5 py-2 text-[12px] text-danger-text" role="alert">
              {error}
            </p>
          ) : null}
        </div>
        <div className="flex justify-end gap-2 border-t border-line px-4 py-3">
          <Button onClick={onCancel} disabled={pending}>
            Cancel
          </Button>
          <Button
            variant={tone === 'danger' ? 'danger' : 'primary'}
            onClick={onConfirm}
            disabled={pending || confirmDisabled}
          >
            {pending ? 'Working\u2026' : confirmLabel}
          </Button>
        </div>
      </div>
    </div>
  );
}
