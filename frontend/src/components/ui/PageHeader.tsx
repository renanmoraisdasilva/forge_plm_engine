import { Link } from 'react-router-dom';
import type { ReactNode } from 'react';

export interface Crumb {
  label: string;
  to?: string;
}

export function Breadcrumbs({ items }: { items: Crumb[] }) {
  return (
    <nav aria-label="Breadcrumb" className="flex h-6 items-center gap-1.5 text-[12px]">
      {items.map((item, index) => {
        const last = index === items.length - 1;
        return (
          <span key={`${item.label}-${index}`} className="flex items-center gap-1.5">
            {index > 0 ? <span className="text-line-strong">/</span> : null}
            {item.to && !last ? (
              <Link to={item.to} className="text-ink-muted hover:text-ink hover:underline">
                {item.label}
              </Link>
            ) : (
              <span className={last ? 'font-semibold text-ink' : 'text-ink-muted'}>{item.label}</span>
            )}
          </span>
        );
      })}
    </nav>
  );
}

export function PageHeader({
  title,
  titleMono = false,
  subtitle,
  actions,
  breadcrumbs,
}: {
  title: string;
  titleMono?: boolean;
  subtitle?: ReactNode;
  actions?: ReactNode;
  breadcrumbs?: Crumb[];
}) {
  return (
    <div className="mb-4">
      {breadcrumbs ? <Breadcrumbs items={breadcrumbs} /> : null}
      <div className="mt-1 flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h1
            className={`${titleMono ? 'mono text-[20px] tracking-tight' : 'text-[24px] leading-8 tracking-[-0.02em]'} font-semibold text-ink`}
          >
            {title}
          </h1>
          {subtitle ? <div className="mt-1 text-[12px] text-ink-muted">{subtitle}</div> : null}
        </div>
        {actions ? <div className="flex flex-wrap items-center gap-2">{actions}</div> : null}
      </div>
    </div>
  );
}
