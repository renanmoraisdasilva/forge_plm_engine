import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ECO_STATUSES } from '../api/ecoApi';
import type { EcoStatus } from '../api/types';
import { EcoModeNote } from '../components/EcoModeNote';
import { Button } from '../components/ui/Button';
import { PageHeader } from '../components/ui/PageHeader';
import { Pager } from '../components/ui/Pager';
import { EmptyBlock, ErrorBlock, LoadingBlock } from '../components/ui/States';
import { StatusChip } from '../components/ui/StatusChip';
import { Timestamp } from '../components/ui/Timestamp';
import { useAsync } from '../lib/useAsync';
import { useApi } from '../state/ApiProvider';

const PAGE_SIZE = 50;

/** Screen C. Built against the frozen contract; live once the endpoints exist. */
export function EcoList() {
  const { eco, ecoMode } = useApi();
  const navigate = useNavigate();

  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<EcoStatus | ''>('');
  const [page, setPage] = useState(1);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setSearch(searchInput.trim());
      setPage(1);
    }, 300);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  const { data, error, loading, reload } = useAsync(
    () => eco.listEcos({ search, status, page, pageSize: PAGE_SIZE }),
    [eco, search, status, page],
  );

  const filtersActive = Boolean(search || status);

  return (
    <div>
      <PageHeader
        title="Engineering Changes"
        subtitle="Every proposed BOM change and its position in the approval and release workflow."
        actions={
          <Button variant="primary" onClick={() => navigate('/ecos/new')}>
            Create ECO
          </Button>
        }
      />

      <EcoModeNote mode={ecoMode} />

      <div className="overflow-hidden rounded border border-line bg-surface">
        <div className="flex h-12 flex-wrap items-center gap-2 border-b border-line px-3">
          <input
            type="search"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder="Search ECO number or title"
            aria-label="Search engineering changes"
            className="h-[30px] w-64 rounded border border-line-strong bg-surface px-2 text-[12px] text-ink placeholder:text-ink-muted focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
          />
          <select
            value={status}
            onChange={(event) => {
              setStatus(event.target.value as EcoStatus | '');
              setPage(1);
            }}
            aria-label="Filter by ECO status"
            className="h-[30px] rounded border border-line-strong bg-surface px-2 text-[12px] text-ink focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
          >
            <option value="">All statuses</option>
            {ECO_STATUSES.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>

          {loading ? <span className="ml-auto text-[11px] text-ink-muted">Loading{'\u2026'}</span> : null}
        </div>

        {error ? (
          <ErrorBlock error={error} onRetry={reload} />
        ) : loading && !data ? (
          <LoadingBlock label="Loading engineering changes" />
        ) : data && data.items.length === 0 ? (
          <EmptyBlock
            title={filtersActive ? 'No ECOs match these filters' : 'No engineering changes yet'}
            description={
              filtersActive
                ? 'Clear the search or choose a different status.'
                : 'Create an ECO to propose a change to a product BOM.'
            }
            action={!filtersActive ? <Button onClick={() => navigate('/ecos/new')}>Create ECO</Button> : undefined}
          />
        ) : data ? (
          <>
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-[13px]">
                <thead>
                  <tr>
                    <th className="table-head px-3">ECO number</th>
                    <th className="table-head px-3">Title</th>
                    <th className="table-head px-3">Product</th>
                    <th className="table-head px-3">Status</th>
                    <th className="table-head px-3">Created</th>
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((item) => (
                    <tr
                      key={item.id}
                      className="table-row h-[34px] cursor-pointer"
                      tabIndex={0}
                      onClick={() => navigate(`/ecos/${item.id}`)}
                      onKeyDown={(event) => {
                        if (event.key === 'Enter' || event.key === ' ') {
                          event.preventDefault();
                          navigate(`/ecos/${item.id}`);
                        }
                      }}
                    >
                      <td className="px-3">
                        <span className="mono font-medium text-ink">{item.ecoNumber}</span>
                      </td>
                      <td className="px-3 text-ink">{item.title}</td>
                      <td className="px-3">
                        <span className="mono text-ink">{item.product.partNumber}</span>
                        <span className="ml-2 text-ink-muted">{item.product.name}</span>
                      </td>
                      <td className="px-3">
                        <StatusChip status={item.status} />
                      </td>
                      <td className="px-3 text-ink-subtle">
                        <Timestamp iso={item.createdAtUtc} />
                      </td>
                    </tr>
                  ))}
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
