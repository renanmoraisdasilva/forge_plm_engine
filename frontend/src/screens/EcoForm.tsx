import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import type {
  BomLine,
  Component,
  CurrentRevisionResponse,
  EcoChangeType,
  EcoDetail,
  EcoLineRequest,
  LifecycleStatus,
  Product,
} from '../api/types';
import { listAllRevisionLines } from '../api/productsApi';
import { Button } from '../components/ui/Button';
import { PageHeader } from '../components/ui/PageHeader';
import { EmptyBlock, ErrorBlock, LoadingBlock } from '../components/ui/States';
import { mapEcoError } from '../lib/ecoErrors';
import { fromLocalInputValue, toLocalInputValue } from '../lib/format';
import { useAsync } from '../lib/useAsync';
import { useActor } from '../state/ActorProvider';
import { useApi } from '../state/ApiProvider';

interface LineDraft {
  key: string;
  changeType: EcoChangeType;
  targetComponentId: string;
  replacementComponentId: string;
  newQuantity: string;
  reason: string;
}

interface FormContextBase {
  key: string;
  product: { id: string; partNumber: string; name: string; lifecycleStatus: LifecycleStatus | null };
  baseRevisionCode: string | null;
  baseLines: BomLine[];
  activeComponents: Component[];
}

interface CreateFormContext extends FormContextBase {
  mode: 'create';
  currentRevision: CurrentRevisionResponse | null;
}

interface EditFormContext extends FormContextBase {
  mode: 'edit';
  existing: EcoDetail;
}

type FormContext = CreateFormContext | EditFormContext;

function blankLine(): LineDraft {
  return {
    key: crypto.randomUUID(),
    changeType: 'ReplaceComponent',
    targetComponentId: '',
    replacementComponentId: '',
    newQuantity: '',
    reason: '',
  };
}

function lineFromExisting(line: EcoDetail['lines'][number]): LineDraft {
  return {
    key: line.id,
    changeType: line.changeType,
    targetComponentId: line.targetComponentId,
    replacementComponentId: line.replacementComponentId ?? '',
    newQuantity: line.newQuantity !== null ? String(line.newQuantity) : '',
    reason: line.reason ?? '',
  };
}

/** Screen E. Draft editing is allowed only while the ECO is Draft. */
export function EcoForm() {
  const { ecoId } = useParams();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const { products, eco } = useApi();
  const { actor } = useActor();

  const [selectedProductId, setSelectedProductId] = useState('');
  const [savedId, setSavedId] = useState<string | null>(null);

  const [title, setTitle] = useState('');
  const [reason, setReason] = useState('');
  const [impact, setImpact] = useState('');
  const [requestedLocal, setRequestedLocal] = useState('');
  const [lines, setLines] = useState<LineDraft[]>([blankLine()]);

  const [saving, setSaving] = useState(false);
  const [saveIntent, setSaveIntent] = useState<'draft' | 'submit' | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [errorCode, setErrorCode] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [linesError, setLinesError] = useState<string | null>(null);
  const [lineErrors, setLineErrors] = useState<Record<string, string>>({});

  const productsQuery = useAsync(async () => (ecoId ? null : products.listProducts({ pageSize: 200 })), [
    products,
    ecoId,
  ]);
  const productOptions = productsQuery.data?.items ?? [];

  // Default the create form to the product passed by "Create ECO" on screen B, or to
  // the first product when the user came from the ECO list.
  useEffect(() => {
    if (ecoId || selectedProductId) return;
    if (productOptions.length === 0) return;
    const requested = searchParams.get('productId');
    const candidate = productOptions.find((product) => product.id === requested) ?? productOptions[0];
    setSelectedProductId(candidate.id);
  }, [ecoId, selectedProductId, productOptions, searchParams]);

  const contextQuery = useAsync<FormContext | null>(async () => {
    if (ecoId) {
      const existing = await eco.getEco(ecoId);
      const [baseLines, components] = await Promise.all([
        listAllRevisionLines(products, existing.baseReleasedRevisionId),
        products.listComponents({ status: 'Active', pageSize: 200 }),
      ]);
      return {
        key: `edit:${ecoId}`,
        mode: 'edit',
        existing,
        product: { ...existing.product, lifecycleStatus: null },
        baseRevisionCode: existing.baseRevisionCode,
        baseLines,
        activeComponents: components.items,
      };
    }

    if (!selectedProductId) return null;

    const [product, currentRevision, components] = await Promise.all([
      products.getProduct(selectedProductId),
      products.getCurrentRevision(selectedProductId),
      products.listComponents({ status: 'Active', pageSize: 200 }),
    ]);

    return {
      key: `create:${selectedProductId}`,
      mode: 'create',
      product,
      currentRevision,
      baseRevisionCode: currentRevision?.revision.revisionCode ?? null,
      baseLines: currentRevision?.lines ?? [],
      activeComponents: components.items,
    };
  }, [products, eco, ecoId, selectedProductId]);

  const context = contextQuery.data;
  const expectedKey = ecoId ? `edit:${ecoId}` : selectedProductId ? `create:${selectedProductId}` : '';
  const contextReady = context !== null && context.key === expectedKey;

  // Populate the form once per product / ECO. Switching product resets the lines,
  // because a target line only means something against one base revision.
  const initializedKey = useRef<string | null>(null);
  useEffect(() => {
    if (!contextReady || !context) return;
    if (initializedKey.current === context.key) return;
    initializedKey.current = context.key;

    setFormError(null);
    setErrorCode(null);
    setFieldErrors({});
    setLinesError(null);
    setLineErrors({});
    setSavedId(null);

    if (context.mode === 'edit') {
      const existing = context.existing;
      setTitle(existing.title);
      setReason(existing.reasonForChange);
      setImpact(existing.impactAssessment ?? '');
      setRequestedLocal(toLocalInputValue(existing.requestedEffectiveAtUtc));
      setLines(existing.lines.map(lineFromExisting));
    } else {
      setLines([blankLine()]);
      setRequestedLocal('');
    }
  }, [context, contextReady]);

  if (productsQuery.error) {
    return (
      <div className="rounded border border-line bg-surface">
        <ErrorBlock error={productsQuery.error} onRetry={productsQuery.reload} />
      </div>
    );
  }

  if (contextQuery.error) {
    return (
      <div className="rounded border border-line bg-surface">
        <ErrorBlock error={contextQuery.error} onRetry={contextQuery.reload} />
      </div>
    );
  }

  if (!ecoId && productsQuery.loading && !productsQuery.data) {
    return (
      <div className="rounded border border-line bg-surface">
        <LoadingBlock label="Loading products" />
      </div>
    );
  }

  if (!ecoId && !productsQuery.loading && productOptions.length === 0) {
    return (
      <div className="rounded border border-line bg-surface">
        <EmptyBlock title="No products" description="An ECO needs an affected product, and no products exist yet." />
      </div>
    );
  }

  if (!contextReady) {
    return (
      <div className="rounded border border-line bg-surface">
        <LoadingBlock label={ecoId ? 'Loading engineering change' : 'Loading base BOM'} />
      </div>
    );
  }

  if (!context) return null;

  if (context.mode === 'edit' && context.existing.status !== 'Draft') {
    return (
      <div>
        <PageHeader
          breadcrumbs={[
            { label: 'Engineering Changes', to: '/ecos' },
            { label: context.existing.ecoNumber, to: `/ecos/${context.existing.id}` },
            { label: 'Edit' },
          ]}
          title={`${context.existing.ecoNumber} cannot be edited`}
          subtitle={`Only a Draft ECO can be edited. This ECO is ${context.existing.status}.`}
          actions={<Button onClick={() => navigate(`/ecos/${context.existing.id}`)}>Open ECO</Button>}
        />
      </div>
    );
  }

  const editContext = context.mode === 'edit' ? context : null;
  const isDraft = editContext ? editContext.existing.status === 'Draft' : true;
  const baseLines = context.baseLines;
  const baseIds = new Set(baseLines.map((line) => line.componentId));
  const replacementOptions = context.activeComponents.filter((component) => !baseIds.has(component.id));
  const targetOptions = baseLines.map((line) => line.component);

  const duplicateTargets = new Set(
    lines
      .map((line) => line.targetComponentId)
      .filter((value, index, all) => value !== '' && all.indexOf(value) !== index),
  );

  const canUseProduct =
    context.mode === 'edit'
      ? true
      : context.product.lifecycleStatus === 'Active' && context.currentRevision !== null;

  const noBaseRevision = context.mode === 'create' && context.currentRevision === null;

  const setLine = (key: string, patch: Partial<LineDraft>) => {
    setLines((current) => current.map((line) => (line.key === key ? { ...line, ...patch } : line)));
  };

  const validate = (): boolean => {
    const nextFieldErrors: Record<string, string> = {};
    const nextLineErrors: Record<string, string> = {};
    let nextLinesError: string | null = null;

    if (!title.trim()) nextFieldErrors.title = 'A title is required.';
    if (!reason.trim()) nextFieldErrors.reasonForChange = 'A reason for change is required.';
    if (lines.length === 0) nextLinesError = 'Add at least one change line.';
    if (duplicateTargets.size > 0) {
      nextLinesError = 'Two lines change the same component. Split them into separate ECOs.';
    }

    for (const line of lines) {
      if (!line.targetComponentId) {
        nextLineErrors[line.key] = 'Choose a target component.';
      } else if (line.changeType === 'ReplaceComponent' && !line.replacementComponentId) {
        nextLineErrors[line.key] = 'Choose a replacement component.';
      } else if (line.changeType === 'ChangeQuantity') {
        const quantity = Number(line.newQuantity);
        if (!Number.isInteger(quantity) || quantity <= 0) {
          nextLineErrors[line.key] = 'The new quantity must be a whole number greater than zero.';
        }
      }
    }

    setFieldErrors(nextFieldErrors);
    setLinesError(nextLinesError);
    setLineErrors(nextLineErrors);
    return Object.keys(nextFieldErrors).length === 0 && !nextLinesError && Object.keys(nextLineErrors).length === 0;
  };

  const buildLineRequests = (): EcoLineRequest[] =>
    lines.map((line) => ({
      changeType: line.changeType,
      targetComponentId: line.targetComponentId,
      replacementComponentId:
        line.changeType === 'ReplaceComponent' ? line.replacementComponentId || null : null,
      newQuantity: line.changeType === 'ChangeQuantity' ? Number(line.newQuantity) : null,
      reason: line.reason.trim() || null,
    }));

  const persist = async (submitAfterSave: boolean) => {
    if (saving) return;
    if (!validate()) return;

    setSaving(true);
    setSaveIntent(submitAfterSave ? 'submit' : 'draft');
    setFormError(null);
    setErrorCode(null);

    const targetId = editContext?.existing.id ?? savedId;

    try {
      let saved: EcoDetail;
      if (targetId) {
        saved = await eco.updateEco(targetId, {
          title: title.trim(),
          reasonForChange: reason.trim(),
          impactAssessment: impact.trim() || null,
          requestedEffectiveAtUtc: fromLocalInputValue(requestedLocal),
          lines: buildLineRequests(),
        });
      } else {
        saved = await eco.createEco({
          productId: context.product.id,
          title: title.trim(),
          reasonForChange: reason.trim(),
          impactAssessment: impact.trim() || null,
          requestedEffectiveAtUtc: fromLocalInputValue(requestedLocal),
          actor,
          lines: buildLineRequests(),
        });
      }
      setSavedId(saved.id);

      if (submitAfterSave) {
        try {
          await eco.submitEco(saved.id, actor);
          navigate(`/ecos/${saved.id}`);
          return;
        } catch (reason) {
          const mapped = mapEcoError(reason);
          setFormError(mapped.form ?? Object.values(mapped.fields)[0] ?? 'The ECO was saved, but submission failed.');
          setErrorCode(mapped.code);
          setFieldErrors((current) => ({ ...current, ...mapped.fields }));
          return;
        }
      }

      navigate(`/ecos/${saved.id}`);
    } catch (reason) {
      const mapped = mapEcoError(reason);
      setFieldErrors((current) => ({ ...current, ...mapped.fields }));
      if (mapped.fields.lines) setLinesError(mapped.fields.lines);
      setFormError(mapped.form);
      setErrorCode(mapped.code);
    } finally {
      setSaving(false);
      setSaveIntent(null);
    }
  };

  const hardBlocked = !canUseProduct || noBaseRevision || duplicateTargets.size > 0 || lines.length === 0;
  const showSavedLink = Boolean(savedId);

  return (
    <div>
      <PageHeader
        breadcrumbs={[
          { label: 'Engineering Changes', to: '/ecos' },
          ...(editContext
            ? [
                { label: editContext.existing.ecoNumber, to: `/ecos/${editContext.existing.id}` },
                { label: 'Edit' },
              ]
            : [{ label: 'New' }]),
        ]}
        title={editContext ? `Edit ${editContext.existing.ecoNumber}` : 'Create ECO'}
        subtitle={
          <span>
            {editContext
              ? 'Draft only. The creator and base revision are fixed once the ECO exists.'
              : 'A proposal against the product\u2019s current released BOM revision.'}
          </span>
        }
        actions={
          <>
            <Button onClick={() => navigate(editContext ? `/ecos/${editContext.existing.id}` : '/ecos')}>
              Cancel
            </Button>
            <Button
              disabled={saving || hardBlocked || !isDraft}
              title={hardBlocked ? 'Complete the product and change lines before saving.' : undefined}
              onClick={() => void persist(false)}
            >
              {saving && saveIntent === 'draft' ? 'Saving\u2026' : 'Save draft'}
            </Button>
            <Button
              variant="primary"
              disabled={saving || hardBlocked || !isDraft}
              title={
                hardBlocked
                  ? 'Complete the product and change lines before submitting.'
                  : 'Submit this ECO for review'
              }
              onClick={() => void persist(true)}
            >
              {saving && saveIntent === 'submit' ? 'Submitting\u2026' : 'Save and submit'}
            </Button>
          </>
        }
      />

      {formError ? (
        <div className="mb-3 rounded border border-danger-line bg-danger-bg/60 px-3 py-2 text-[12px] text-danger-text" role="alert">
          <div className="flex flex-wrap items-center gap-2">
            <span>{formError}</span>
            {errorCode ? (
              <code className="mono rounded border border-danger-line bg-surface px-1.5 py-0.5 text-[11px]">
                {errorCode}
              </code>
            ) : null}
            {showSavedLink && savedId ? (
              <Link to={`/ecos/${savedId}`} className="font-semibold underline">
                Open the saved draft
              </Link>
            ) : null}
          </div>
        </div>
      ) : null}

      {!canUseProduct ? (
        <div className="mb-3 rounded border border-warning-line bg-warning-bg/60 px-3 py-2 text-[12px] text-warning">
          {noBaseRevision
            ? 'This product has no released BOM revision. An ECO must be based on a current released configuration.'
            : 'Only an Active product can receive an engineering change.'}
        </div>
      ) : null}

      <div className="space-y-4">
        <section className="overflow-hidden rounded border border-line bg-surface">
          <h2 className="border-b border-line px-3 py-2 text-[13px] font-semibold text-ink">Change request</h2>
          <div className="grid gap-3 px-3 py-3 md:grid-cols-2">
            <label className="block text-[12px] text-ink-subtle md:col-span-2">
              Title <span className="text-danger">*</span>
              <input
                value={title}
                onChange={(event) => setTitle(event.target.value)}
                maxLength={200}
                className="mt-1 h-[30px] w-full rounded border border-line-strong bg-surface px-2 text-[12px] text-ink focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
              />
              {fieldErrors.title ? <span className="mt-1 block text-[11px] text-danger-text">{fieldErrors.title}</span> : null}
            </label>

            <label className="block text-[12px] text-ink-subtle">
              Affected product <span className="text-danger">*</span>
              {editContext ? (
                <div className="mono mt-1 flex h-[30px] items-center rounded border border-line bg-muted px-2 text-ink">
                  {editContext.product.partNumber}
                  <span className="ml-2 font-sans text-ink-muted">{editContext.product.name}</span>
                </div>
              ) : (
                <select
                  value={selectedProductId}
                  onChange={(event) => {
                    setSelectedProductId(event.target.value);
                    setSavedId(null);
                  }}
                  className="mt-1 h-[30px] w-full rounded border border-line-strong bg-surface px-2 text-[12px] text-ink focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
                >
                  {productOptions.map((product: Product) => (
                    <option key={product.id} value={product.id}>
                      {product.partNumber} {'\u2014'} {product.name} ({product.lifecycleStatus})
                    </option>
                  ))}
                </select>
              )}
              {fieldErrors.product ? <span className="mt-1 block text-[11px] text-danger-text">{fieldErrors.product}</span> : null}
            </label>

            <div className="text-[12px] text-ink-subtle">
              Base released revision
              <div className="mt-1 flex h-[30px] items-center rounded border border-line bg-muted px-2">
                {context.baseRevisionCode ? (
                  <span className="mono font-semibold text-ink">{context.baseRevisionCode}</span>
                ) : (
                  <span className="text-ink-muted">None released</span>
                )}
                <span className="ml-2 text-[11px] text-ink-muted">
                  {baseLines.length} BOM {baseLines.length === 1 ? 'line' : 'lines'}
                </span>
              </div>
            </div>

            <label className="block text-[12px] text-ink-subtle md:col-span-2">
              Reason for change <span className="text-danger">*</span>
              <textarea
                value={reason}
                onChange={(event) => setReason(event.target.value)}
                rows={3}
                maxLength={4000}
                className="mt-1 w-full rounded border border-line-strong bg-surface px-2 py-1.5 text-[12px] text-ink focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
              />
              {fieldErrors.reasonForChange ? (
                <span className="mt-1 block text-[11px] text-danger-text">{fieldErrors.reasonForChange}</span>
              ) : null}
            </label>

            <label className="block text-[12px] text-ink-subtle">
              Impact assessment
              <textarea
                value={impact}
                onChange={(event) => setImpact(event.target.value)}
                rows={2}
                className="mt-1 w-full rounded border border-line-strong bg-surface px-2 py-1.5 text-[12px] text-ink focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
              />
            </label>

            <label className="block text-[12px] text-ink-subtle">
              Requested effective date
              <input
                type="datetime-local"
                value={requestedLocal}
                onChange={(event) => setRequestedLocal(event.target.value)}
                className="mt-1 h-[30px] w-full rounded border border-line-strong bg-surface px-2 text-[12px] text-ink focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
              />
              <span className="mt-1 block text-[11px] text-ink-muted">
                Stored as UTC and displayed in local time. If this date is still in the future at release, the prototype asks you to confirm immediate effectivity.
              </span>
            </label>
          </div>
        </section>

        <section className="overflow-hidden rounded border border-line bg-surface">
          <div className="flex items-center justify-between border-b border-line px-3 py-2">
            <div>
              <h2 className="text-[13px] font-semibold text-ink">Change lines</h2>
              <p className="text-[11px] text-ink-muted">
                Replace a component or change its quantity. Each component may appear in only one line.
              </p>
            </div>
            <Button
              disabled={baseLines.length === 0}
              title={baseLines.length === 0 ? 'There is no base BOM to change.' : 'Add a change line'}
              onClick={() => setLines((current) => [...current, blankLine()])}
            >
              Add line
            </Button>
          </div>

          {linesError ? (
            <p className="border-b border-danger-line bg-danger-bg/50 px-3 py-1.5 text-[11px] text-danger-text">{linesError}</p>
          ) : null}

          {baseLines.length === 0 ? (
            <EmptyBlock
              title="No base BOM"
              description="This product has no released BOM revision, so there is nothing to change."
            />
          ) : (
            <div className="divide-y divide-line">
              {lines.map((line, index) => {
                const usedElsewhere = new Set(
                  lines
                    .filter((candidate) => candidate.key !== line.key && candidate.targetComponentId)
                    .map((candidate) => candidate.targetComponentId),
                );
                const lineError = lineErrors[line.key];

                return (
                  <div key={line.key} className="px-3 py-3">
                    <div className="mb-2 flex items-center justify-between">
                      <span className="text-[11px] font-semibold uppercase tracking-[0.06em] text-ink-muted">
                        Line {index + 1}
                      </span>
                      <Button
                        variant="ghost"
                        disabled={lines.length === 1}
                        title={lines.length === 1 ? 'An ECO needs at least one change line.' : 'Remove this line'}
                        onClick={() => setLines((current) => current.filter((candidate) => candidate.key !== line.key))}
                      >
                        Remove
                      </Button>
                    </div>

                    <div className="grid gap-3 md:grid-cols-3">
                      <label className="block text-[12px] text-ink-subtle">
                        Change type
                        <select
                          value={line.changeType}
                          onChange={(event) => {
                            const changeType = event.target.value as EcoChangeType;
                            setLine(line.key, {
                              changeType,
                              replacementComponentId: changeType === 'ReplaceComponent' ? line.replacementComponentId : '',
                              newQuantity: changeType === 'ChangeQuantity' ? line.newQuantity || '1' : '',
                            });
                          }}
                          className="mt-1 h-[30px] w-full rounded border border-line-strong bg-surface px-2 text-[12px] text-ink focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
                        >
                          <option value="ReplaceComponent">Replace component</option>
                          <option value="ChangeQuantity">Change quantity</option>
                        </select>
                      </label>

                      <label className="block text-[12px] text-ink-subtle">
                        Target component <span className="text-danger">*</span>
                        <select
                          value={line.targetComponentId}
                          onChange={(event) => setLine(line.key, { targetComponentId: event.target.value })}
                          className="mt-1 h-[30px] w-full rounded border border-line-strong bg-surface px-2 text-[12px] text-ink focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
                        >
                          <option value="">Select a component</option>
                          {targetOptions.map((component) => (
                            <option
                              key={component.id}
                              value={component.id}
                              disabled={usedElsewhere.has(component.id)}
                            >
                              {component.partNumber} {'\u2014'} {component.name}
                            </option>
                          ))}
                        </select>
                      </label>

                      {line.changeType === 'ReplaceComponent' ? (
                        <label className="block text-[12px] text-ink-subtle">
                          Replacement component <span className="text-danger">*</span>
                          <select
                            value={line.replacementComponentId}
                            onChange={(event) => setLine(line.key, { replacementComponentId: event.target.value })}
                            className="mt-1 h-[30px] w-full rounded border border-line-strong bg-surface px-2 text-[12px] text-ink focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
                          >
                            <option value="">Select a replacement</option>
                            {replacementOptions.map((component) => (
                              <option key={component.id} value={component.id}>
                                {component.partNumber} {'\u2014'} {component.name}
                              </option>
                            ))}
                          </select>
                        </label>
                      ) : (
                        <label className="block text-[12px] text-ink-subtle">
                          New quantity <span className="text-danger">*</span>
                          <input
                            type="number"
                            min={1}
                            step={1}
                            value={line.newQuantity}
                            onChange={(event) => setLine(line.key, { newQuantity: event.target.value })}
                            className="mt-1 h-[30px] w-full rounded border border-line-strong bg-surface px-2 text-[12px] text-ink focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
                          />
                        </label>
                      )}

                      <label className="block text-[12px] text-ink-subtle md:col-span-3">
                        Line reason
                        <input
                          value={line.reason}
                          onChange={(event) => setLine(line.key, { reason: event.target.value })}
                          className="mt-1 h-[30px] w-full rounded border border-line-strong bg-surface px-2 text-[12px] text-ink focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
                        />
                      </label>
                    </div>

                    {lineError ? (
                      <p className="mt-1 text-[11px] text-danger-text">{lineError}</p>
                    ) : null}
                  </div>
                );
              })}
            </div>
          )}
        </section>

        <p className="text-[11px] text-ink-muted">
          Saving keeps this ECO in Draft. Submitting sends it for review. Approval creates an Approved revision;
          release is a separate action. Acting as <span className="font-semibold text-ink">{actor}</span>.
        </p>
      </div>
    </div>
  );
}
