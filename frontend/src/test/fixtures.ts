import { vi } from 'vitest';
import type { ApiBundle } from '../api';
import type { EcoApi } from '../api/ecoApi';
import type { ProductsApi } from '../api/productsApi';
import type {
  AuditEvent,
  BomLine,
  BomRevision,
  EcoDetail,
  EcoSummary,
  PagedResponse,
  Product,
} from '../api/types';

export function paged<T>(
  items: T[],
  overrides: Partial<PagedResponse<T>> = {},
): PagedResponse<T> {
  return {
    items,
    total: items.length,
    page: 1,
    pageSize: 50,
    ...overrides,
  };
}

export function product(overrides: Partial<Product> = {}): Product {
  return {
    id: 'product-1',
    partNumber: 'FP-100',
    name: 'Forge Frame',
    category: 'Mechanical',
    lifecycleStatus: 'Active',
    createdAtUtc: '2026-10-20T10:00:00Z',
    updatedAtUtc: '2026-10-20T10:00:00Z',
    ...overrides,
  };
}

export function bomRevision(overrides: Partial<BomRevision> = {}): BomRevision {
  return {
    id: 'revision-1',
    productId: 'product-1',
    revisionCode: 'A',
    status: 'Released',
    effectiveAtUtc: '2026-10-20T10:00:00Z',
    releasedAtUtc: '2026-10-20T10:00:00Z',
    createdAtUtc: '2026-10-20T09:00:00Z',
    createdBy: 'Alex Morgan',
    sourceEcoId: null,
    lineCount: 1,
    ...overrides,
  };
}

export function bomLine(overrides: Partial<BomLine> = {}): BomLine {
  return {
    id: 'bom-line-1',
    componentId: 'component-1',
    quantity: 2,
    unit: 'ea',
    component: {
      id: 'component-1',
      partNumber: 'C-100',
      name: 'Old bracket',
      lifecycleStatus: 'Active',
    },
    ...overrides,
  };
}

export function ecoSummary(overrides: Partial<EcoSummary> = {}): EcoSummary {
  return {
    id: 'eco-1',
    ecoNumber: 'ECO-0001',
    title: 'Replace the bracket',
    productId: 'product-1',
    product: { id: 'product-1', partNumber: 'FP-100', name: 'Forge Frame' },
    status: 'Submitted',
    createdBy: 'Alex Morgan',
    createdAtUtc: '2026-10-20T10:00:00Z',
    lineCount: 1,
    ...overrides,
  };
}

export function ecoDetail(overrides: Partial<EcoDetail> = {}): EcoDetail {
  return {
    ...ecoSummary(),
    baseReleasedRevisionId: 'revision-1',
    baseRevisionCode: 'A',
    reasonForChange: 'The old bracket is obsolete.',
    impactAssessment: null,
    requestedEffectiveAtUtc: null,
    submittedAtUtc: '2026-10-20T11:00:00Z',
    decidedAtUtc: null,
    decidedBy: null,
    reviewerComment: null,
    approvedRevisionId: null,
    lines: [
      {
        id: 'eco-line-1',
        changeType: 'ReplaceComponent',
        targetComponentId: 'component-1',
        target: { id: 'component-1', partNumber: 'C-100', name: 'Old bracket' },
        replacementComponentId: 'component-2',
        replacement: { id: 'component-2', partNumber: 'C-200', name: 'New bracket' },
        newQuantity: null,
        reason: null,
      },
    ],
    ...overrides,
  };
}

export function auditEvent(overrides: Partial<AuditEvent> = {}): AuditEvent {
  return {
    id: 'audit-1',
    occurredAtUtc: '2026-10-20T10:05:00Z',
    actor: 'Alex Morgan',
    action: 'EcoCreated',
    entityType: 'EngineeringChangeOrder',
    entityId: 'eco-1',
    details: { lineCount: 1 },
    ...overrides,
  };
}

/**
 * A complete ApiBundle with inert defaults. Tests override only the methods that
 * matter, which keeps each test focused on one screen behaviour instead of on DTOs.
 */
export function fakeApi({
  products = {},
  eco = {},
}: {
  products?: Partial<ProductsApi>;
  eco?: Partial<EcoApi>;
} = {}): ApiBundle {
  return {
    products: {
      listProducts: vi.fn().mockResolvedValue(paged([])),
      getProduct: vi.fn().mockResolvedValue(product()),
      listComponents: vi.fn().mockResolvedValue(paged([])),
      getComponent: vi.fn(),
      listBomRevisions: vi.fn().mockResolvedValue(paged([])),
      getBomRevision: vi.fn().mockResolvedValue(bomRevision()),
      getCurrentRevision: vi.fn().mockResolvedValue(null),
      listRevisionLines: vi.fn().mockResolvedValue(paged([])),
      ...products,
    } as ProductsApi,
    eco: {
      listEcos: vi.fn().mockResolvedValue(paged([])),
      getEco: vi.fn().mockResolvedValue(ecoDetail()),
      createEco: vi.fn().mockResolvedValue(ecoDetail()),
      updateEco: vi.fn().mockResolvedValue(ecoDetail()),
      submitEco: vi.fn().mockResolvedValue(ecoDetail()),
      approveEco: vi.fn().mockResolvedValue(ecoDetail()),
      rejectEco: vi.fn().mockResolvedValue(ecoDetail()),
      releaseEco: vi.fn().mockResolvedValue(ecoDetail()),
      listAudit: vi.fn().mockResolvedValue(paged([])),
      ...eco,
    } as EcoApi,
  };
}
