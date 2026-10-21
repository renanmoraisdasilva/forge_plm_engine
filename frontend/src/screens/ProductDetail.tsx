import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import type { EcoSummary, PagedResponse } from '../api/types';
import { Button } from '../components/ui/Button';
import { PageHeader } from '../components/ui/PageHeader';
import { EmptyBlock, ErrorBlock, LoadingBlock } from '../components/ui/States';
import { StatusChip } from '../components/ui/StatusChip';
import { Timestamp } from '../components/ui/Timestamp';
import { useAsync } from '../lib/useAsync';
import { useApi } from '../state/ApiProvider';

type Tab = 'bom' | 'revisions' | 'related';

const TABS: Array<{ id: Tab; label: string }> = [
  { id: 'bom', label: 'BOM' },
  { id: 'revisions', label: 'Revisions' },
  { id: 'related', label: 'Related Changes' },
];

/** Screen B. Header, three tabs, Create ECO. Live reads only. */
export function ProductDetail() {
  const { productId = '' } = useParams();
  const { products, ecoMode } = useApi();
  const navigate = useNavigate();
  const [tab, setTab] = useState<Tab>('bom');

  const { data, error, loading, reload } = useAsync(async () => {
    const [product, revisions, current] = await Promise.all([
      products.getProduct(productId),
      products.listBomRevisions(productId, { pageSize: 200 }),
      products.getCurrentRevision(productId),
    ]);
    return { product, revisions, current };
  }, [products, productId]);

  if (loading && !data) {
    return (
      <div className="rounded border border-line bg-surface">
        <LoadingBlock label="Loading product" />
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

  const { product, revisions, current } = data;
  const canCreateEco = product.lifecycleStatus === 'Active' && current !== null;
  const createEcoTitle = !canCreateEco
    ? product.lifecycleStatus !== 'Active'
      ? 'Only an active product can receive an engineering change.'
      : 'This product has no released BOM revision to change.'
    : 'Create an engineering change order for this product';

  return (
    <div>
      <PageHeader
        breadcrumbs={[{ label: 'Products', to: '/products' }, { label: product.partNumber }]}
        title={product.name}
        subtitle={
          <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
            <span className="mono text-[13px] font-medium text-ink">{product.partNumber}</span>
            <StatusChip status={product.lifecycleStatus} />
            <span className="text-ink-muted">Category {product.category}</span>
            <span className="text-ink-muted">
              Created <Timestamp iso={product.createdAtUtc} />
            </span>
            <span className="text-ink-muted">
              Current released revision{' '}
              {current ? (
                <span className="mono font-semibold text-ink">{current.revision.revisionCode}</span>
              ) : (
                <span className="text-ink-muted">none</span>
              )}
            </span>
          </div>
        }
        actions={
          <Button
            variant="primary"
            disabled={!canCreateEco}
            title={createEcoTitle}
            onClick={() => navigate(`/ecos/new?productId=${product.id}`)}
          >
            Create ECO
          </Button>
        }
      />

      <div className="overflow-hidden rounded border border-line bg-surface">
        <div role="tablist" aria-label="Product detail tabs" className="flex h-[38px] items-end gap-4 border-b border-line px-3">
          {TABS.map((item) => (
            <button
              key={item.id}
              role="tab"
              type="button"
              aria-selected={tab === item.id}
              onClick={() => setTab(item.id)}
              className={`h-[34px] border-b-2 px-0.5 text-[12px] transition-colors ${
                tab === item.id
                  ? 'border-primary font-semibold text-ink'
                  : 'border-transparent text-ink-muted hover:text-ink'
              }`}
            >
              {item.label}
            </button>
          ))}
        </div>

        {tab === 'bom' ? (
          current ? (
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-[13px]">
                <thead>
                  <tr>
                    <th className="table-head px-3">Component</th>
                    <th className="table-head px-3">Part number</th>
                    <th className="table-head px-3 text-right">Quantity</th>
                    <th className="table-head px-3">Unit</th>
                    <th className="table-head px-3">Revision</th>
                    <th className="table-head px-3">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {current.lines.map((line) => (
                    <tr key={line.id} className="table-row h-[32px]">
                      <td className="px-3 text-ink">{line.component.name}</td>
                      <td className="px-3">
                        <span className="mono font-medium text-ink">{line.component.partNumber}</span>
                      </td>
                      <td className="px-3 text-right tabular-nums text-ink">{line.quantity}</td>
                      <td className="px-3 text-ink-subtle">{line.unit}</td>
                      <td className="px-3">
                        <span className="mono text-ink">{current.revision.revisionCode}</span>
                      </td>
                      <td className="px-3">
                        <StatusChip status={current.revision.status} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <EmptyBlock
              title="No released BOM revision"
              description="This product has no current configuration. An ECO cannot be created until a revision is released."
            />
          )
        ) : null}

        {tab === 'revisions' ? (
          revisions.items.length === 0 ? (
            <EmptyBlock title="No BOM revisions" description="This product has no revision history yet." />
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full border-collapse text-[13px]">
                <thead>
                  <tr>
                    <th className="table-head px-3">Revision</th>
                    <th className="table-head px-3">Status</th>
                    <th className="table-head px-3">Effective</th>
                    <th className="table-head px-3">Created</th>
                    <th className="table-head px-3">Created by</th>
                    <th className="table-head px-3 text-right">Lines</th>
                    <th className="table-head px-3">Source ECO</th>
                  </tr>
                </thead>
                <tbody>
                  {revisions.items.map((revision) => (
                    <tr key={revision.id} className="table-row h-[32px]">
                      <td className="px-3">
                        <span className="mono font-semibold text-ink">{revision.revisionCode}</span>
                      </td>
                      <td className="px-3">
                        <StatusChip status={revision.status} />
                      </td>
                      <td className="px-3 text-ink-subtle">
                        <Timestamp iso={revision.effectiveAtUtc} />
                      </td>
                      <td className="px-3 text-ink-subtle">
                        <Timestamp iso={revision.createdAtUtc} />
                      </td>
                      <td className="px-3 text-ink-subtle">{revision.createdBy}</td>
                      <td className="px-3 text-right tabular-nums text-ink">{revision.lineCount}</td>
                      <td className="px-3">
                        {revision.sourceEcoId ? (
                          <Link
                            to={`/ecos/${revision.sourceEcoId}`}
                            className="text-primary hover:underline"
                          >
                            View ECO
                          </Link>
                        ) : (
                          <span className="text-ink-muted">{'\u2014'}</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        ) : null}

        {tab === 'related' ? <RelatedChanges productId={product.id} ecoMode={ecoMode} /> : null}
      </div>
    </div>
  );
}

function RelatedChanges({ productId, ecoMode }: { productId: string; ecoMode: 'live' | 'mock' }) {
  const { eco } = useApi();
  const navigate = useNavigate();
  const { data, error, loading, reload } = useAsync<PagedResponse<EcoSummary>>(
    () => eco.listEcos({ productId, pageSize: 50 }),
    [eco, productId],
  );

  return (
    <div>
      {ecoMode === 'mock' ? (
        <div className="border-b border-line bg-muted px-3 py-1.5 text-[11px] text-ink-muted">
          ECO endpoints are not live yet; this list uses the contract mock adapter.
        </div>
      ) : null}

      {error ? (
        <ErrorBlock error={error} onRetry={reload} />
      ) : loading && !data ? (
        <LoadingBlock label="Loading related changes" />
      ) : data && data.items.length === 0 ? (
        <EmptyBlock
          title="No engineering changes"
          description="No ECOs reference this product yet."
          action={
            <Button onClick={() => navigate(`/ecos/new?productId=${productId}`)}>Create ECO</Button>
          }
        />
      ) : data ? (
        <table className="w-full border-collapse text-[13px]">
          <thead>
            <tr>
              <th className="table-head px-3">ECO number</th>
              <th className="table-head px-3">Title</th>
              <th className="table-head px-3">Status</th>
              <th className="table-head px-3">Created</th>
            </tr>
          </thead>
          <tbody>
            {data.items.map((item) => (
              <tr
                key={item.id}
                className="table-row h-[32px] cursor-pointer"
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
                  <StatusChip status={item.status} />
                </td>
                <td className="px-3 text-ink-subtle">
                  <Timestamp iso={item.createdAtUtc} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
    </div>
  );
}
