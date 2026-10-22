import { HttpClient } from './http';
import type {
  ApproveEcoRequest,
  AuditEvent,
  CreateEcoRequest,
  EcoDetail,
  EcoStatus,
  EcoSummary,
  ListAuditParams,
  ListEcosParams,
  PagedResponse,
  RejectEcoRequest,
  ReleaseEcoRequest,
  UpdateEcoRequest,
} from './types';

/** The ECO surface of docs/IMPLEMENTATION.md, served by the live API. */
export interface EcoApi {
  listEcos(params?: ListEcosParams): Promise<PagedResponse<EcoSummary>>;
  getEco(ecoId: string): Promise<EcoDetail>;
  createEco(request: CreateEcoRequest): Promise<EcoDetail>;
  updateEco(ecoId: string, request: UpdateEcoRequest): Promise<EcoDetail>;
  submitEco(ecoId: string, actor: string): Promise<EcoDetail>;
  approveEco(ecoId: string, request: ApproveEcoRequest): Promise<EcoDetail>;
  rejectEco(ecoId: string, request: RejectEcoRequest): Promise<EcoDetail>;
  releaseEco(ecoId: string, request: ReleaseEcoRequest): Promise<EcoDetail>;
  listAudit(params?: ListAuditParams): Promise<PagedResponse<AuditEvent>>;
}

export const ECO_STATUSES: readonly EcoStatus[] = ['Draft', 'Submitted', 'Approved', 'Rejected', 'Applied'];

export class LiveEcoApi implements EcoApi {
  private readonly http: HttpClient;

  constructor(http: HttpClient) {
    this.http = http;
  }

  listEcos(params: ListEcosParams = {}) {
    return this.http.get<PagedResponse<EcoSummary>>('/ecos', params);
  }

  getEco(ecoId: string) {
    return this.http.get<EcoDetail>(`/ecos/${ecoId}`);
  }

  createEco(request: CreateEcoRequest) {
    return this.http.post<EcoDetail>('/ecos', request);
  }

  updateEco(ecoId: string, request: UpdateEcoRequest) {
    return this.http.put<EcoDetail>(`/ecos/${ecoId}`, request);
  }

  submitEco(ecoId: string, actor: string) {
    return this.http.post<EcoDetail>(`/ecos/${ecoId}/submit`, { actor });
  }

  approveEco(ecoId: string, request: ApproveEcoRequest) {
    return this.http.post<EcoDetail>(`/ecos/${ecoId}/approve`, request);
  }

  rejectEco(ecoId: string, request: RejectEcoRequest) {
    return this.http.post<EcoDetail>(`/ecos/${ecoId}/reject`, request);
  }

  releaseEco(ecoId: string, request: ReleaseEcoRequest) {
    return this.http.post<EcoDetail>(`/ecos/${ecoId}/release`, request);
  }

  listAudit(params: ListAuditParams = {}) {
    return this.http.get<PagedResponse<AuditEvent>>('/audit', params);
  }
}
