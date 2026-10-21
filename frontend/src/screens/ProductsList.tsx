import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import type { BomRevision, LifecycleStatus } from '../api/types';
import { PageHeader } from '../components/ui/PageHeader';
import { Pager } from '../components/ui/Pager';
import { EmptyBlock, ErrorBlock, LoadingBlock } from '../components/ui/States';
import { StatusChip } from '../components/ui/StatusChip';
import { useAsync } from '../lib/useAsync';
import { useApi } from '../state/ApiProvider';

const PAGE_SIZE = 50;

const STATUS_OPTIONS: Array<{ value: LifecycleStatus | ''; label: string }> = [
  { value: '', label: 'All lifecycles' },
  { value: 'Draft', label: 'Draft' },
  { value: 'Active', label: 'Active' },
  { value: 'Retired', label: 'Retired' },
];

/** Screen A. Live API only — there is no product mock. */
export function ProductsList() {
  const { products } = useApi();
  const navigate = useNavigate();

  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<LifecycleStatus | ''>('');
  const [page, setPage] = useState(1);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setSearch(searchInput.trim());
      setPage(1);
    }, 300);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  const { data, error, loading, reload } = useAsync(async () => {
    const paged = await products.listProducts({ search, status, page, pageSize: PAGE_SIZE });
    const currentRevisions = new Map<string, BomRevision | null>();

    await Promise.all(
      paged.items.map(async (product) => {
        try {
          const current = await products.getCurrentRevision(product.id);
          currentRevisions.set(product.id, current?.revision ?? null);
        } catch {
          // A row without its revision still renders; the column shows an em dash.
          currentRevisions.set(product.id, null);
        }
      }),
    );

    return { ...paged, currentRevisions };
  }, [products, search, status, page]);

  const filtersActive = Boolean(search || status);

  return (
    <div>
      <PageHeader title="Products" subtitle="Controlled items and their current released BOM revision." />

      <div className="overflow-hidden rounded border border-line bg-surface">
        <div className="flex h-12 flex-wrap items-center gap-2 border-b border-line px-3">
          <input
            type="search"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder="Search part number or name"
            aria-label="Search products"
            className="h-[30px] w-64 rounded border border-line-strong bg-surface px-2 text-[12px] text-ink placeholder:text-ink-muted focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
          />
          <select
            value={status}
            onChange={(event) => {
              setStatus(event.target.value as LifecycleStatus | '');
              setPage(1);
            }}
            aria-label="Filter by lifecycle status"
            className="h-[30px] rounded border border-line-strong bg-surface px-2 text-[12px] text-ink focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
          >
            {STATUS_OPTIONS.map((option) => (
              <option key={option.value || 'all'} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>

          {loading ? <span className="ml-auto text-[11px] text-ink-muted">Loading{'\u2026'}</span> : null}
        </div>

        {error ? (
          <ErrorBlock error={error} onRetry={reload} />
        ) : loading && !data ? (
          <LoadingBlock label="Loading products" />
        ) : data && data.items.length === 0 ? (
          <EmptyBlock
            title={filtersActive ? 'No products match these filters' : 'No products yet'}
            description={
              filtersActive
                ? 'Clear the search or choose a different lifecycle status.'
                : 'Products are created by the API and the seed data, not by this UI in v1.'
            }
          />
        ) : data ? (
          <>
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-[13px]">
                <thead>
                  <tr>
                    <th className="table-head px-3">Part number</th>
                    <th className="table-head px-3">Name</th>
                    <th className="table-head px-3">Category</th>
                    <th className="table-head px-3">Current released revision</th>
                    <th className="table-head px-3">Lifecycle</th>
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((product) => {
                    const current = data.currentRevisions.get(product.id) ?? null;
                    return (
                      <tr
                        key={product.id}
                        className="table-row h-[34px] cursor-pointer"
                        tabIndex={0}
                        onClick={() => navigate(`/products/${product.id}`)}
                        onKeyDown={(event) => {
                          if (event.key === 'Enter' || event.key === ' ') {
                            event.preventDefault();
                            navigate(`/products/${product.id}`);
                          }
                        }}
                      >
                        <td className="px-3">
                          <span className="mono font-medium text-ink">{product.partNumber}</span>
                        </td>
                        <td className="px-3 text-ink">{product.name}</td>
                        <td className="px-3 text-ink-subtle">{product.category}</td>
                        <td className="px-3">
                          {current ? (
                            <span className="mono text-ink" title={`${current.status} revision`}>
                              {current.revisionCode}
                            </span>
                          ) : (
                            <span className="text-ink-muted">{'\u2014'}</span>
                          )}
                        </td>
                        <td className="px-3">
                          <StatusChip status={product.lifecycleStatus} />
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
            <Pager
              total={data.total}
              page={data.page}
              pageSize={data.pageSize}
              onPage={setPage}
              disabled={loading}
            />
          </>
        ) : null}
      </div>
    </div>
  );
}
