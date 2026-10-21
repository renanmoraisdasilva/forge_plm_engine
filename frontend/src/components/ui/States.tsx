import type { ReactNode } from 'react';
import { ApiError, describeError } from '../../api/http';
import { Button } from './Button';

export function LoadingBlock({ label = 'Loading\u2026' }: { label?: string }) {
  return (
    <div className="flex items-center gap-2 px-3 py-8 text-[12px] text-ink-muted" role="status">
      <span className="h-3 w-3 animate-spin rounded-full border-2 border-line-strong border-t-primary" />
      {label}
    </div>
  );
}

export function ErrorBlock({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const code = error instanceof ApiError ? error.machineCode : null;
  return (
    <div className="m-3 rounded border border-danger-line bg-danger-bg/40 px-3 py-3" role="alert">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-[12px] font-semibold text-danger-text">Request failed</span>
        {code ? (
          <code className="mono rounded border border-danger-line bg-surface px-1.5 py-0.5 text-[11px] text-danger-text">
            {code}
          </code>
        ) : null}
      </div>
      <p className="mt-1 text-[12px] text-ink-subtle">{describeError(error)}</p>
      {onRetry ? (
        <div className="mt-2">
          <Button onClick={onRetry}>Retry</Button>
        </div>
      ) : null}
    </div>
  );
}

export function EmptyBlock({
  title,
  description,
  action,
}: {
  title: string;
  description?: string;
  action?: ReactNode;
}) {
  return (
    <div className="flex flex-col items-center justify-center gap-2 px-6 py-12 text-center">
      <p className="text-[13px] font-semibold text-ink">{title}</p>
      {description ? <p className="max-w-md text-[12px] text-ink-muted">{description}</p> : null}
      {action ? <div className="mt-2">{action}</div> : null}
    </div>
  );
}
