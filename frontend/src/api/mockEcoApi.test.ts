import { beforeEach, describe, expect, it } from 'vitest';
import { ApiError } from './http';
import { MockEcoApi } from './mockEcoApi';
import type { ProductsApi, PageParams, ComponentListParams, ProductListParams } from './productsApi';
import type {
  BomLine,
  BomRevision,
  Component,
  CurrentRevisionResponse,
  PagedResponse,
  Product,
} from './types';

const PRODUCT_ID = '00000001-0000-0000-0000-000000000001';
const REVISION_ID = '00000002-0000-0000-0000-000000000002';
const CPU = '00000010-0000-0000-0000-000000000010';
const MB020 = '00000011-0000-0000-0000-000000000011';
const MB021 = '00000012-0000-0000-0000-000000000012';
const RAM = '00000013-0000-0000-0000-000000000013';
const RETIRED = '00000019-0000-0000-0000-000000000019';

const product: Product = {
  id: PRODUCT_ID,
  partNumber: 'SERVER-100',
  name: 'Compute Server',
  category: 'Server',
  lifecycleStatus: 'Active',
  createdAtUtc: '2026-01-15T00:00:00Z',
  updatedAtUtc: '2026-01-15T00:00:00Z',
};

function component(id: string, partNumber: string, name: string, status: Component['lifecycleStatus'] = 'Active'): Component {
  return { id, partNumber, name, category: 'Test', lifecycleStatus: status, createdAtUtc: '2026-01-15T00:00:00Z' };
}

const components: Component[] = [
  component(CPU, 'CPU-001', 'Xeon Processor'),
  component(MB020, 'MB-020', 'Server Motherboard'),
  component(MB021, 'MB-021', 'Updated Server Motherboard'),
  component(RAM, 'RAM-032', '32GB Memory Module'),
  component(RETIRED, 'OLD-001', 'Retired Part', 'Retired'),
];

const revision: BomRevision = {
  id: REVISION_ID,
  productId: PRODUCT_ID,
  revisionCode: 'B',
  status: 'Released',
  effectiveAtUtc: '2026-01-15T00:00:00Z',
  createdAtUtc: '2026-01-15T00:00:00Z',
  createdBy: 'System',
  sourceEcoId: null,
  lineCount: 3,
};

const lines: BomLine[] = [
  { id: 'line-1', componentId: CPU, quantity: 2, unit: 'Each', component: components[0] },
  { id: 'line-2', componentId: MB020, quantity: 1, unit: 'Each', component: components[1] },
  { id: 'line-3', componentId: RAM, quantity: 8, unit: 'Each', component: components[3] },
];

function paged<T>(items: T[], page = 1, pageSize = 50): PagedResponse<T> {
  const start = (Math.max(1, page) - 1) * pageSize;
  return { items: items.slice(start, start + pageSize), total: items.length, page, pageSize };
}

class FakeCatalog implements ProductsApi {
  private readonly hasReleasedRevision: boolean;

  constructor(hasReleasedRevision = true) {
    this.hasReleasedRevision = hasReleasedRevision;
  }

  async listProducts(_params: ProductListParams = {}) {
    return paged([product]);
  }

  async getProduct(productId: string) {
    if (productId !== PRODUCT_ID) throw new ApiError(404, { title: 'Product not found.' });
    return product;
  }

  async listComponents(params: ComponentListParams = {}) {
    let items = components;
    if (params.status) items = items.filter((entry) => entry.lifecycleStatus === params.status);
    if (params.search) {
      const term = params.search.toLowerCase();
      items = items.filter(
        (entry) => entry.partNumber.toLowerCase().includes(term) || entry.name.toLowerCase().includes(term),
      );
    }
    return paged(items, params.page, params.pageSize);
  }

  async getComponent(componentId: string) {
    const found = components.find((entry) => entry.id === componentId);
    if (!found) throw new ApiError(404, { title: 'Component not found.' });
    return found;
  }

  async listBomRevisions(_productId: string, params: PageParams = {}) {
    return paged([revision], params.page, params.pageSize);
  }

  async getBomRevision(revisionId: string) {
    if (revisionId !== REVISION_ID) throw new ApiError(404, { title: 'Revision not found.' });
    return revision;
  }

  async getCurrentRevision(_productId: string): Promise<CurrentRevisionResponse | null> {
    if (!this.hasReleasedRevision) return null;
    return { revision, lines };
  }

  async listRevisionLines(_revisionId: string, params: PageParams = {}) {
    return paged(lines, params.page, params.pageSize);
  }
}

function fixedClock() {
  let tick = 0;
  return () => new Date(Date.parse('2026-10-21T10:00:00Z') + tick++ * 60_000);
}

const baseRequest = {
  productId: PRODUCT_ID,
  title: 'Replace the motherboard',
  reasonForChange: 'The MB-020 supply is ending.',
  impactAssessment: null,
  requestedEffectiveAtUtc: null,
  actor: 'Alex Morgan',
};

async function draftReplacement(api: MockEcoApi) {
  return api.createEco({
    ...baseRequest,
    lines: [
      {
        changeType: 'ReplaceComponent',
        targetComponentId: MB020,
        replacementComponentId: MB021,
        newQuantity: null,
        reason: null,
      },
    ],
  });
}

describe('MockEcoApi — contract behaviour', () => {
  let api: MockEcoApi;

  beforeEach(() => {
    api = new MockEcoApi(new FakeCatalog(), fixedClock());
  });

  it('creates a Draft against the current released revision and generates the ECO number', async () => {
    const created = await draftReplacement(api);

    expect(created.status).toBe('Draft');
    expect(created.ecoNumber).toBe('ECO-2026-0001');
    expect(created.baseReleasedRevisionId).toBe(REVISION_ID);
    expect(created.baseRevisionCode).toBe('B');
    expect(created.createdBy).toBe('Alex Morgan');
    expect(created.lines).toHaveLength(1);
    expect(created.lines[0].target.partNumber).toBe('MB-020');
    expect(created.lines[0].replacement?.partNumber).toBe('MB-021');
    expect(created.approvedRevisionId).toBeNull();
  });

  it('runs create → submit → approve → release as distinct actions', async () => {
    const created = await draftReplacement(api);
    const submitted = await api.submitEco(created.id, 'Alex Morgan');
    expect(submitted.status).toBe('Submitted');
    expect(submitted.submittedAtUtc).not.toBeNull();

    const approved = await api.approveEco(created.id, { actor: 'Jordan Lee', comment: 'Looks correct.' });
    expect(approved.status).toBe('Approved');
    expect(approved.approvedRevisionId).toBeTruthy();
    expect(approved.decidedBy).toBe('Jordan Lee');
    expect(approved.reviewerComment).toBe('Looks correct.');

    const released = await api.releaseEco(created.id, {
      actor: 'Jordan Lee',
      effectiveAtUtc: '2026-11-01T00:00:00Z',
    });
    expect(released.status).toBe('Applied');
  });

  it('writes the audit trail in the order the actions happened', async () => {
    const created = await draftReplacement(api);
    await api.submitEco(created.id, 'Alex Morgan');
    await api.approveEco(created.id, { actor: 'Jordan Lee', comment: null });

    const audit = await api.listAudit({
      entityType: 'EngineeringChangeOrder',
      entityId: created.id,
      pageSize: 200,
    });

    expect(audit.items.map((event) => event.action).sort()).toEqual(
      ['EcoApproved', 'EcoCreated', 'EcoSubmitted'].sort(),
    );
    expect(audit.items.every((event) => event.entityId === created.id)).toBe(true);
    expect(audit.items.find((event) => event.action === 'EcoApproved')?.actor).toBe('Jordan Lee');
  });

  it('rejects approval and release from the wrong status with invalid-transition', async () => {
    const created = await draftReplacement(api);

    await expect(api.approveEco(created.id, { actor: 'Jordan Lee', comment: null })).rejects.toMatchObject({
      status: 409,
      conflictType: 'invalid-transition',
    });

    await api.submitEco(created.id, 'Alex Morgan');

    await expect(
      api.releaseEco(created.id, { actor: 'Jordan Lee', effectiveAtUtc: '2026-11-01T00:00:00Z' }),
    ).rejects.toMatchObject({ status: 409, conflictType: 'invalid-transition' });
  });

  it('refuses to edit or submit an ECO that is no longer Draft', async () => {
    const created = await draftReplacement(api);
    await api.submitEco(created.id, 'Alex Morgan');

    await expect(
      api.updateEco(created.id, {
        title: 'Changed',
        reasonForChange: 'Changed',
        impactAssessment: null,
        requestedEffectiveAtUtc: null,
        lines: [],
      }),
    ).rejects.toMatchObject({ status: 409, conflictType: 'invalid-transition' });

    await expect(api.submitEco(created.id, 'Alex Morgan')).rejects.toMatchObject({
      status: 409,
      conflictType: 'invalid-transition',
    });
  });

  it('requires a non-blank rejection reason and creates no revision', async () => {
    const created = await draftReplacement(api);
    await api.submitEco(created.id, 'Alex Morgan');

    await expect(
      api.rejectEco(created.id, { actor: 'Jordan Lee', rejectionReason: '   ' }),
    ).rejects.toMatchObject({ status: 400, code: 'rejection-reason-required' });

    const rejected = await api.rejectEco(created.id, {
      actor: 'Jordan Lee',
      rejectionReason: 'Wrong footprint.',
    });
    expect(rejected.status).toBe('Rejected');
    expect(rejected.approvedRevisionId).toBeNull();
    expect(rejected.reviewerComment).toBe('Wrong footprint.');
  });

  it('rejects duplicate target components', async () => {
    await expect(
      api.createEco({
        ...baseRequest,
        lines: [
          {
            changeType: 'ReplaceComponent',
            targetComponentId: MB020,
            replacementComponentId: MB021,
            newQuantity: null,
            reason: null,
          },
          {
            changeType: 'ChangeQuantity',
            targetComponentId: MB020,
            replacementComponentId: null,
            newQuantity: 2,
            reason: null,
          },
        ],
      }),
    ).rejects.toMatchObject({ status: 400, code: 'duplicate-target-component' });
  });

  it('rejects a target outside the base revision and a non-positive quantity', async () => {
    await expect(
      api.createEco({
        ...baseRequest,
        lines: [
          {
            changeType: 'ChangeQuantity',
            targetComponentId: MB021,
            replacementComponentId: null,
            newQuantity: 2,
            reason: null,
          },
        ],
      }),
    ).rejects.toMatchObject({ status: 400, code: 'target-component-not-in-base-revision' });

    await expect(
      api.createEco({
        ...baseRequest,
        lines: [
          {
            changeType: 'ChangeQuantity',
            targetComponentId: RAM,
            replacementComponentId: null,
            newQuantity: 0,
            reason: null,
          },
        ],
      }),
    ).rejects.toMatchObject({ status: 400, code: 'non-positive-quantity' });
  });

  it('rejects a replacement that is not active', async () => {
    await expect(
      api.createEco({
        ...baseRequest,
        lines: [
          {
            changeType: 'ReplaceComponent',
            targetComponentId: MB020,
            replacementComponentId: RETIRED,
            newQuantity: null,
            reason: null,
          },
        ],
      }),
    ).rejects.toMatchObject({ status: 400, code: 'component-not-active' });
  });

  it('requires title, actor and at least one line', async () => {
    await expect(api.createEco({ ...baseRequest, title: '  ', lines: [] })).rejects.toMatchObject({
      status: 400,
      code: 'blank-title',
    });

    await expect(api.createEco({ ...baseRequest, actor: '', lines: [] })).rejects.toMatchObject({
      status: 400,
      code: 'blank-actor',
    });

    await expect(api.createEco({ ...baseRequest, lines: [] })).rejects.toMatchObject({
      status: 400,
      code: 'eco-lines-required',
    });
  });

  it('returns no-released-revision when the product has no current BOM', async () => {
    const empty = new MockEcoApi(new FakeCatalog(false), fixedClock());
    await expect(draftReplacement(empty)).rejects.toMatchObject({
      status: 409,
      conflictType: 'no-released-revision',
    });
  });

  it('filters, searches and pages the ECO list', async () => {
    await draftReplacement(api);
    await api.createEco({
      ...baseRequest,
      title: 'Increase memory',
      lines: [
        {
          changeType: 'ChangeQuantity',
          targetComponentId: RAM,
          replacementComponentId: null,
          newQuantity: 16,
          reason: null,
        },
      ],
    });

    const all = await api.listEcos({ pageSize: 50 });
    expect(all.total).toBe(2);
    expect(all.items[0].ecoNumber).toBe('ECO-2026-0002');

    const searched = await api.listEcos({ search: 'memory' });
    expect(searched.items).toHaveLength(1);
    expect(searched.items[0].title).toBe('Increase memory');

    const pageTwo = await api.listEcos({ page: 2, pageSize: 1 });
    expect(pageTwo.items).toHaveLength(1);
    expect(pageTwo.items[0].ecoNumber).toBe('ECO-2026-0001');

    const drafts = await api.listEcos({ status: 'Draft' });
    expect(drafts.total).toBe(2);
  });

  it('rejects an unrecognised status filter instead of returning an empty list', async () => {
    await expect(api.listEcos({ status: 'Cancelled' as never })).rejects.toMatchObject({
      status: 400,
      code: 'invalid-filter-value',
    });
  });

  it('404s for an unknown ECO', async () => {
    await expect(api.getEco('missing')).rejects.toMatchObject({ status: 404 });
  });
});
