import { ApiError, problemError } from './http';
import { listAllRevisionLines, type ProductsApi } from './productsApi';
import type { EcoApi } from './ecoApi';
import type {
  ApproveEcoRequest,
  AuditEvent,
  BomLine,
  BomRevision,
  ComponentSummary,
  CreateEcoRequest,
  EcoDetail,
  EcoLine,
  EcoLineRequest,
  EcoStatus,
  EcoSummary,
  ListAuditParams,
  ListEcosParams,
  PagedResponse,
  RejectEcoRequest,
  ReleaseEcoRequest,
  UpdateEcoRequest,
} from './types';

/**
 * In-memory implementation of docs/ECO-API-CONTRACT.md.
 *
 * It is deliberately shaped like the server rather than like the screens: it resolves
 * the product and its current released revision, generates the ECO number, enforces the
 * status matrix, validates that targets are in the base BOM, and creates an `Approved`
 * revision on approval with release as a separate call. That is what makes it safe to
 * develop C/D/E against, and it means the real endpoints can replace it without the UI
 * changing.
 *
 * Product, component and BOM reads are NOT mocked. The adapter is handed the live
 * `ProductsApi` and uses the seeded data, so the demo's SERVER-100 revision B is real.
 *
 * Known mock-only limitation: revisions created here are not written to PostgreSQL, so
 * the product detail screen (which reads the live API) will not list them. Once the real
 * endpoints land, the mock is deleted and the limitation disappears.
 */

interface StoredRevision {
  revision: BomRevision;
  lines: BomLine[];
}

const MAX_PAGE_SIZE = 200;

function clone<T>(value: T): T {
  return structuredClone(value);
}

function pageOf<T>(items: T[], page = 1, pageSize = 50): PagedResponse<T> {
  const effectivePage = Math.max(1, Math.trunc(page) || 1);
  const effectivePageSize = Math.min(MAX_PAGE_SIZE, Math.max(1, Math.trunc(pageSize) || 50));
  const start = (effectivePage - 1) * effectivePageSize;
  return {
    items: items.slice(start, start + effectivePageSize),
    total: items.length,
    page: effectivePage,
    pageSize: effectivePageSize,
  };
}

function nextRevisionCode(current: string): string {
  const code = current.trim().toUpperCase();
  if (code.length !== 1 || code < 'A' || code >= 'Z') {
    throw problemError(409, { conflictType: 'revision-limit-reached' }, 'Revision limit reached.', 'Revision codes run from A to Z.');
  }
  return String.fromCharCode(code.charCodeAt(0) + 1);
}

function summary(component: ComponentSummary): EcoDetail['product'] {
  return { id: component.id, partNumber: component.partNumber, name: component.name };
}

export class MockEcoApi implements EcoApi {
  private readonly ecos = new Map<string, EcoDetail>();
  private readonly revisions = new Map<string, StoredRevision>();
  private readonly catalog: ProductsApi;
  private readonly clock: () => Date;
  private audits: AuditEvent[] = [];
  private sequenceByYear = new Map<number, number>();

  constructor(catalog: ProductsApi, clock: () => Date = () => new Date()) {
    this.catalog = catalog;
    this.clock = clock;
  }

  async listEcos(params: ListEcosParams = {}): Promise<PagedResponse<EcoSummary>> {
    const search = params.search?.trim().toLowerCase();
    const status = params.status;

    if (status && !['Draft', 'Submitted', 'Approved', 'Rejected', 'Applied'].includes(status)) {
      throw problemError(400, { code: 'invalid-filter-value' }, 'Invalid status filter.', `'${status}' is not an ECO status.`);
    }

    const items = [...this.ecos.values()]
      .filter((eco) => !status || eco.status === status)
      .filter((eco) => !params.productId || eco.productId === params.productId)
      .filter(
        (eco) =>
          !search ||
          eco.ecoNumber.toLowerCase().includes(search) ||
          eco.title.toLowerCase().includes(search),
      )
      .sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc) || b.ecoNumber.localeCompare(a.ecoNumber))
      .map((eco) => this.toSummary(eco));

    return pageOf(items, params.page, params.pageSize);
  }

  async getEco(ecoId: string): Promise<EcoDetail> {
    return clone(this.requireEco(ecoId));
  }

  async createEco(request: CreateEcoRequest): Promise<EcoDetail> {
    const actor = this.requireActor(request.actor);
    const title = this.requireTitle(request.title);

    const product = await this.catalog.getProduct(request.productId);
    const current = await this.catalog.getCurrentRevision(product.id);
    if (!current) {
      throw problemError(
        409,
        { conflictType: 'no-released-revision' },
        'No released revision.',
        'An ECO is created against a product\u2019s current released BOM, and this product has none.',
      );
    }

    const lines = await this.validateLines(request.lines, current.revision.id);

    const now = this.nowIso();
    const ecoNumber = this.nextEcoNumber();
    const eco: EcoDetail = {
      id: crypto.randomUUID(),
      ecoNumber,
      title,
      productId: product.id,
      product: { id: product.id, partNumber: product.partNumber, name: product.name },
      status: 'Draft',
      createdBy: actor,
      createdAtUtc: now,
      lineCount: lines.length,
      baseReleasedRevisionId: current.revision.id,
      baseRevisionCode: current.revision.revisionCode,
      reasonForChange: request.reasonForChange?.trim() ?? '',
      impactAssessment: request.impactAssessment?.trim() || null,
      requestedEffectiveAtUtc: request.requestedEffectiveAtUtc || null,
      submittedAtUtc: null,
      decidedAtUtc: null,
      decidedBy: null,
      reviewerComment: null,
      approvedRevisionId: null,
      lines,
    };

    this.ecos.set(eco.id, eco);
    this.recordAudit(eco, 'EcoCreated', actor, {
      ecoNumber,
      productPartNumber: product.partNumber,
      lineCount: lines.length,
    });

    return clone(eco);
  }

  async updateEco(ecoId: string, request: UpdateEcoRequest): Promise<EcoDetail> {
    const eco = this.requireEco(ecoId);
    if (eco.status !== 'Draft') {
      throw this.invalidTransition(eco.status, 'edited');
    }

    const title = this.requireTitle(request.title);
    const lines = await this.validateLines(request.lines, eco.baseReleasedRevisionId);

    eco.title = title;
    eco.reasonForChange = request.reasonForChange?.trim() ?? '';
    eco.impactAssessment = request.impactAssessment?.trim() || null;
    eco.requestedEffectiveAtUtc = request.requestedEffectiveAtUtc || null;
    eco.lines = lines;
    eco.lineCount = lines.length;

    return clone(eco);
  }

  async submitEco(ecoId: string, actorInput: string): Promise<EcoDetail> {
    const eco = this.requireEco(ecoId);
    const actor = this.requireActor(actorInput);
    if (eco.status !== 'Draft') {
      throw this.invalidTransition(eco.status, 'submitted');
    }
    if (eco.lines.length === 0) {
      throw this.rule('eco-lines-required', 'An ECO needs at least one change line.');
    }

    await this.requireValidStoredLines(eco);

    eco.status = 'Submitted';
    eco.submittedAtUtc = this.nowIso();
    this.recordAudit(eco, 'EcoSubmitted', actor, { ecoNumber: eco.ecoNumber });

    return clone(eco);
  }

  async approveEco(ecoId: string, request: ApproveEcoRequest): Promise<EcoDetail> {
    const eco = this.requireEco(ecoId);
    const actor = this.requireActor(request.actor);
    if (eco.status !== 'Submitted') {
      throw this.invalidTransition(eco.status, 'approved');
    }

    const current = await this.catalog.getCurrentRevision(eco.productId);
    if (!current || current.revision.id !== eco.baseReleasedRevisionId) {
      throw this.staleBase();
    }

    await this.requireValidStoredLines(eco);

    const baseLines = await listAllRevisionLines(this.catalog, eco.baseReleasedRevisionId);
    const revisionCode = nextRevisionCode(eco.baseRevisionCode);
    const now = this.nowIso();

    const newLines: BomLine[] = baseLines.map((line) => ({
      id: crypto.randomUUID(),
      componentId: line.componentId,
      quantity: line.quantity,
      unit: line.unit,
      component: summary(line.component),
    }));

    for (const change of eco.lines) {
      const line = newLines.find((candidate) => candidate.componentId === change.targetComponentId);
      if (!line) {
        throw this.rule('target-component-not-in-base-revision', `${change.target.partNumber} is not in the base BOM.`);
      }

      if (change.changeType === 'ReplaceComponent' && change.replacement) {
        line.componentId = change.replacement.id;
        line.component = summary(change.replacement);
      } else if (change.changeType === 'ChangeQuantity' && change.newQuantity !== null) {
        line.quantity = change.newQuantity;
      }
    }

    // Defensive: the domain allows each component at most once per revision.
    const componentIds = new Set(newLines.map((line) => line.componentId));
    if (componentIds.size !== newLines.length) {
      throw this.rule('duplicate-target-component', 'The proposed changes would put the same component in the revision twice.');
    }

    const revisionId = crypto.randomUUID();
    this.revisions.set(revisionId, {
      revision: {
        id: revisionId,
        productId: eco.productId,
        revisionCode,
        status: 'Approved',
        effectiveAtUtc: null,
        createdAtUtc: now,
        createdBy: actor,
        sourceEcoId: eco.id,
        lineCount: newLines.length,
      },
      lines: newLines,
    });

    eco.status = 'Approved';
    eco.decidedAtUtc = now;
    eco.decidedBy = actor;
    eco.reviewerComment = request.comment?.trim() || null;
    eco.approvedRevisionId = revisionId;

    this.recordAudit(eco, 'EcoApproved', actor, {
      ecoNumber: eco.ecoNumber,
      revisionCode,
      revisionId,
    });

    return clone(eco);
  }

  async rejectEco(ecoId: string, request: RejectEcoRequest): Promise<EcoDetail> {
    const eco = this.requireEco(ecoId);
    const actor = this.requireActor(request.actor);
    if (eco.status !== 'Submitted') {
      throw this.invalidTransition(eco.status, 'rejected');
    }

    const reason = request.rejectionReason?.trim();
    if (!reason) {
      throw this.rule('rejection-reason-required', 'Rejecting an ECO requires a reason.');
    }

    eco.status = 'Rejected';
    eco.decidedAtUtc = this.nowIso();
    eco.decidedBy = actor;
    eco.reviewerComment = reason;

    this.recordAudit(eco, 'EcoRejected', actor, {
      ecoNumber: eco.ecoNumber,
      rejectionReason: reason,
    });

    return clone(eco);
  }

  async releaseEco(ecoId: string, request: ReleaseEcoRequest): Promise<EcoDetail> {
    const eco = this.requireEco(ecoId);
    const actor = this.requireActor(request.actor);
    if (eco.status !== 'Approved') {
      throw this.invalidTransition(eco.status, 'released');
    }

    const effectiveAt = request.effectiveAtUtc?.trim();
    if (!effectiveAt || Number.isNaN(Date.parse(effectiveAt))) {
      throw this.rule('effective-date-required', 'Release requires an effective date.');
    }

    const current = await this.catalog.getCurrentRevision(eco.productId);
    if (!current || current.revision.id !== eco.baseReleasedRevisionId) {
      throw this.staleBase();
    }

    const approved = eco.approvedRevisionId ? this.revisions.get(eco.approvedRevisionId) : undefined;
    if (!approved || approved.revision.status !== 'Approved') {
      throw this.invalidTransition(eco.status, 'released');
    }

    const baseRevision = this.revisions.get(eco.baseReleasedRevisionId) ?? {
      revision: clone(current.revision),
      lines: clone(current.lines),
    };
    baseRevision.revision.status = 'Superseded';
    this.revisions.set(baseRevision.revision.id, baseRevision);

    approved.revision.status = 'Released';
    approved.revision.effectiveAtUtc = effectiveAt;

    eco.status = 'Applied';

    this.recordAudit(eco, 'RevisionReleased', actor, {
      ecoNumber: eco.ecoNumber,
      revisionCode: approved.revision.revisionCode,
      effectiveAtUtc: effectiveAt,
      supersededRevisionCode: baseRevision.revision.revisionCode,
    });

    return clone(eco);
  }

  async listAudit(params: ListAuditParams = {}): Promise<PagedResponse<AuditEvent>> {
    const items = this.audits
      .filter((event) => !params.entityType || event.entityType === params.entityType)
      .filter((event) => !params.entityId || event.entityId === params.entityId)
      .sort((a, b) => b.occurredAtUtc.localeCompare(a.occurredAtUtc));

    return pageOf(items, params.page, params.pageSize);
  }

  // ------------------------------------------------------------------------------------
  // Validation mirrors the codes in the frozen contract so the form renders the same
  // messages against the mock and the real backend.

  private async validateLines(requests: EcoLineRequest[] | undefined, baseRevisionId: string): Promise<EcoLine[]> {
    if (!requests || requests.length === 0) {
      throw this.rule('eco-lines-required', 'An ECO needs at least one change line.');
    }

    const baseLines = await listAllRevisionLines(this.catalog, baseRevisionId);
    const baseById = new Map(baseLines.map((line) => [line.componentId, line]));

    const seen = new Set<string>();
    const lines: EcoLine[] = [];

    for (const request of requests) {
      if (!request.targetComponentId) {
        throw this.rule('target-component-required', 'Choose the component this change applies to.');
      }
      if (seen.has(request.targetComponentId)) {
        throw this.rule(
          'duplicate-target-component',
          'Two lines change the same component. Split them into separate ECOs.',
        );
      }
      seen.add(request.targetComponentId);

      const baseLine = baseById.get(request.targetComponentId);
      if (!baseLine) {
        throw this.rule(
          'target-component-not-in-base-revision',
          'The target component is not part of the base revision.',
        );
      }

      if (request.changeType === 'ReplaceComponent') {
        if (!request.replacementComponentId) {
          throw this.rule('replacement-component-required', 'A replacement component is required.');
        }
        if (request.replacementComponentId === request.targetComponentId) {
          throw this.rule('replacement-must-differ-from-target', 'The replacement must differ from the target component.');
        }

        let replacement;
        try {
          replacement = await this.catalog.getComponent(request.replacementComponentId);
        } catch (error) {
          if (error instanceof ApiError && error.status === 404) {
            throw this.rule('replacement-component-required', 'The replacement component does not exist.');
          }
          throw error;
        }

        if (replacement.lifecycleStatus !== 'Active') {
          throw this.rule('component-not-active', `${replacement.partNumber} is not active and cannot be added to a revision.`);
        }

        lines.push({
          id: crypto.randomUUID(),
          changeType: 'ReplaceComponent',
          targetComponentId: request.targetComponentId,
          target: summary(baseLine.component),
          replacementComponentId: replacement.id,
          replacement: summary(replacement),
          newQuantity: null,
          reason: request.reason?.trim() || null,
        });
      } else if (request.changeType === 'ChangeQuantity') {
        if (
          request.newQuantity === null ||
          request.newQuantity === undefined ||
          !Number.isInteger(request.newQuantity) ||
          request.newQuantity <= 0
        ) {
          throw this.rule('non-positive-quantity', 'The new quantity must be a whole number greater than zero.');
        }

        lines.push({
          id: crypto.randomUUID(),
          changeType: 'ChangeQuantity',
          targetComponentId: request.targetComponentId,
          target: summary(baseLine.component),
          replacementComponentId: null,
          replacement: null,
          newQuantity: request.newQuantity,
          reason: request.reason?.trim() || null,
        });
      } else {
        throw this.rule('invalid-change-type', 'Unknown change type.');
      }
    }

    return lines;
  }

  private async requireValidStoredLines(eco: EcoDetail): Promise<void> {
    const baseLines = await listAllRevisionLines(this.catalog, eco.baseReleasedRevisionId);
    const baseById = new Map(baseLines.map((line) => [line.componentId, line]));
    const seen = new Set<string>();

    for (const line of eco.lines) {
      if (!baseById.has(line.targetComponentId)) {
        throw this.rule('target-component-not-in-base-revision', `${line.target.partNumber} is no longer in the base BOM.`);
      }
      if (seen.has(line.targetComponentId)) {
        throw this.rule('duplicate-target-component', 'Two lines change the same component.');
      }
      seen.add(line.targetComponentId);

      if (line.changeType === 'ReplaceComponent' && line.replacementComponentId) {
        try {
          const replacement = await this.catalog.getComponent(line.replacementComponentId);
          if (replacement.lifecycleStatus !== 'Active') {
            throw this.rule('component-not-active', `${replacement.partNumber} is not active.`);
          }
        } catch (error) {
          if (error instanceof ApiError && error.status === 404) {
            throw this.rule('replacement-component-required', 'The replacement component no longer exists.');
          }
          throw error;
        }
      }
    }
  }

  // ------------------------------------------------------------------------------------

  private requireEco(ecoId: string): EcoDetail {
    const eco = this.ecos.get(ecoId);
    if (!eco) {
      throw problemError(404, {}, 'ECO not found.', `No ECO exists with id ${ecoId}.`);
    }
    return eco;
  }

  private requireActor(actor: string | undefined): string {
    const trimmed = actor?.trim();
    if (!trimmed) {
      throw this.rule('blank-actor', 'An actor is required.');
    }
    return trimmed;
  }

  private requireTitle(title: string | undefined): string {
    const trimmed = title?.trim();
    if (!trimmed) {
      throw this.rule('blank-title', 'A title is required.');
    }
    return trimmed;
  }

  private invalidTransition(status: EcoStatus, verb: string): ApiError {
    return problemError(
      409,
      { conflictType: 'invalid-transition' },
      'Invalid status transition.',
      `An ECO in ${status} cannot be ${verb}.`,
    );
  }

  private staleBase(): ApiError {
    return problemError(
      409,
      { conflictType: 'stale-base-revision' },
      'Stale base revision.',
      'Another ECO released a newer revision. Create a new ECO against the current revision.',
    );
  }

  private rule(code: string, detail: string): ApiError {
    return problemError(400, { code }, 'Validation failed.', detail);
  }

  private toSummary(eco: EcoDetail): EcoSummary {
    return clone({
      id: eco.id,
      ecoNumber: eco.ecoNumber,
      title: eco.title,
      productId: eco.productId,
      product: eco.product,
      status: eco.status,
      createdBy: eco.createdBy,
      createdAtUtc: eco.createdAtUtc,
      lineCount: eco.lineCount,
    });
  }

  private nowIso(): string {
    return this.clock().toISOString();
  }

  private nextEcoNumber(): string {
    const year = this.clock().getUTCFullYear();
    const next = (this.sequenceByYear.get(year) ?? 0) + 1;
    this.sequenceByYear.set(year, next);
    return `ECO-${year}-${String(next).padStart(4, '0')}`;
  }

  private recordAudit(
    eco: EcoDetail,
    action: AuditEvent['action'],
    actor: string,
    details: Record<string, unknown>,
  ): void {
    this.audits.push({
      id: crypto.randomUUID(),
      occurredAtUtc: this.nowIso(),
      actor,
      action,
      entityType: 'EngineeringChangeOrder',
      entityId: eco.id,
      details,
    });
  }
}
