export type LifecycleStatus = 'Draft' | 'Active' | 'Retired';

export type BomRevisionStatus = 'Draft' | 'Approved' | 'Released' | 'Superseded';

export type EcoStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected' | 'Applied';

export type EcoChangeType = 'ReplaceComponent' | 'ChangeQuantity';

export type AuditAction =
  | 'EcoCreated'
  | 'EcoSubmitted'
  | 'EcoApproved'
  | 'EcoRejected'
  | 'RevisionReleased'
  | 'ProductCreated'
  | 'ComponentCreated';

/** Shared by every list endpoint (contract "Conventions"). */
export interface PagedResponse<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface Product {
  id: string;
  partNumber: string;
  name: string;
  category: string;
  lifecycleStatus: LifecycleStatus;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface ComponentSummary {
  id: string;
  partNumber: string;
  name: string;
  lifecycleStatus?: LifecycleStatus;
}

export interface Component extends ComponentSummary {
  category: string;
  lifecycleStatus: LifecycleStatus;
  createdAtUtc: string;
}

export interface BomLine {
  id: string;
  componentId: string;
  quantity: number;
  unit: string;
  component: ComponentSummary;
}

export interface BomRevision {
  id: string;
  productId: string;
  revisionCode: string;
  status: BomRevisionStatus;
  effectiveAtUtc: string | null;
  createdAtUtc: string;
  createdBy: string;
  sourceEcoId: string | null;
  lineCount: number;
}

export interface CurrentRevisionResponse {
  revision: BomRevision;
  lines: BomLine[];
}

export interface EcoProductRef {
  id: string;
  partNumber: string;
  name: string;
}

/** EcoSummaryDto — contract DTOs. */
export interface EcoSummary {
  id: string;
  ecoNumber: string;
  title: string;
  productId: string;
  product: EcoProductRef;
  status: EcoStatus;
  createdBy: string;
  createdAtUtc: string;
  lineCount: number;
}

export interface EcoLine {
  id: string;
  changeType: EcoChangeType;
  targetComponentId: string;
  target: EcoProductRef;
  replacementComponentId: string | null;
  replacement: EcoProductRef | null;
  newQuantity: number | null;
  reason: string | null;
}

/** EcoDetailDto — EcoSummaryDto plus these fields. */
export interface EcoDetail extends EcoSummary {
  baseReleasedRevisionId: string;
  baseRevisionCode: string;
  reasonForChange: string;
  impactAssessment: string | null;
  requestedEffectiveAtUtc: string | null;
  submittedAtUtc: string | null;
  decidedAtUtc: string | null;
  decidedBy: string | null;
  reviewerComment: string | null;
  approvedRevisionId: string | null;
  lines: EcoLine[];
}

export interface AuditEvent {
  id: string;
  occurredAtUtc: string;
  actor: string;
  action: AuditAction | string;
  entityType: string;
  entityId: string;
  details: Record<string, unknown>;
}

export interface EcoLineRequest {
  changeType: EcoChangeType;
  targetComponentId: string;
  replacementComponentId: string | null;
  newQuantity: number | null;
  reason: string | null;
}

export interface CreateEcoRequest {
  productId: string;
  title: string;
  reasonForChange: string;
  impactAssessment: string | null;
  requestedEffectiveAtUtc: string | null;
  actor: string;
  lines: EcoLineRequest[];
}

/** PUT body: same as POST minus productId and actor. */
export interface UpdateEcoRequest {
  title: string;
  reasonForChange: string;
  impactAssessment: string | null;
  requestedEffectiveAtUtc: string | null;
  lines: EcoLineRequest[];
}

export interface ActorRequest {
  actor: string;
}

export interface ApproveEcoRequest extends ActorRequest {
  comment: string | null;
}

export interface RejectEcoRequest extends ActorRequest {
  rejectionReason: string;
}

export interface ReleaseEcoRequest extends ActorRequest {
  effectiveAtUtc: string;
}

export interface ListEcosParams {
  search?: string;
  status?: EcoStatus | '';
  productId?: string;
  page?: number;
  pageSize?: number;
}

export interface ListAuditParams {
  entityType?: 'Product' | 'Component' | 'BomRevision' | 'EngineeringChangeOrder' | '';
  entityId?: string;
  page?: number;
  pageSize?: number;
}
