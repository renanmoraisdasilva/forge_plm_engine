import { Button } from './Button';

interface PagerProps {
  total: number;
  page: number;
  pageSize: number;
  onPage: (page: number) => void;
  disabled?: boolean;
}

/** The standard pager only: range, previous, next. No page-size picker or jump controls. */
export function Pager({ total, page, pageSize, onPage, disabled = false }: PagerProps) {
  const pageCount = Math.max(1, Math.ceil(total / pageSize));
  const first = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const last = Math.min(total, page * pageSize);

  return (
    <div className="flex h-11 items-center justify-between border-t border-line px-3 text-[12px] text-ink-subtle">
      <span>
        Showing {first}
        {'\u2013'}
        {last} of {total}
      </span>
      <div className="flex items-center gap-2">
        <Button disabled={disabled || page <= 1} onClick={() => onPage(page - 1)}>
          Previous
        </Button>
        <span className="tabular-nums">
          Page {page} of {pageCount}
        </span>
        <Button disabled={disabled || page >= pageCount} onClick={() => onPage(page + 1)}>
          Next
        </Button>
      </div>
    </div>
  );
}
