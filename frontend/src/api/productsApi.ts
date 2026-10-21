import { HttpClient, ApiError } from './http';
import type {
  BomLine,
  BomRevision,
  Component,
  CurrentRevisionResponse,
  LifecycleStatus,
  PagedResponse,
  Product,
} from './types';

export interface ProductListParams {
  search?: string;
  status?: LifecycleStatus | '';
  page?: number;
  pageSize?: number;
}

export interface ComponentListParams {
  search?: string;
  status?: LifecycleStatus | '';
  page?: number;
  pageSize?: number;
}

export interface PageParams {
  page?: number;
  pageSize?: number;
}

/**
 * Reads that already exist live (Slice 3). Kept separate from the ECO client so the
 * mock adapter can use this same catalog without pretending the product endpoints are
 * mocked — they are not.
 */
export interface ProductsApi {
  listProducts(params?: ProductListParams): Promise<PagedResponse<Product>>;
  getProduct(productId: string): Promise<Product>;

  listComponents(params?: ComponentListParams): Promise<PagedResponse<Component>>;
  getComponent(componentId: string): Promise<Component>;

  listBomRevisions(productId: string, params?: PageParams): Promise<PagedResponse<BomRevision>>;
  getBomRevision(revisionId: string): Promise<BomRevision>;
  getCurrentRevision(productId: string): Promise<CurrentRevisionResponse | null>;
  listRevisionLines(revisionId: string, params?: PageParams): Promise<PagedResponse<BomLine>>;
}

export class LiveProductsApi implements ProductsApi {
  private readonly http: HttpClient;

  constructor(http: HttpClient) {
    this.http = http;
  }

  listProducts(params: ProductListParams = {}) {
    return this.http.get<PagedResponse<Product>>('/products', params);
  }

  getProduct(productId: string) {
    return this.http.get<Product>(`/products/${productId}`);
  }

  listComponents(params: ComponentListParams = {}) {
    return this.http.get<PagedResponse<Component>>('/components', params);
  }

  getComponent(componentId: string) {
    return this.http.get<Component>(`/components/${componentId}`);
  }

  listBomRevisions(productId: string, params: PageParams = {}) {
    return this.http.get<PagedResponse<BomRevision>>(`/products/${productId}/bom-revisions`, params);
  }

  getBomRevision(revisionId: string) {
    return this.http.get<BomRevision>(`/bom-revisions/${revisionId}`);
  }

  /**
   * `GET .../bom-revisions/current` answers 404 when the product exists but has no
   * released revision. That is a renderable state, not an error, so it becomes `null`.
   */
  async getCurrentRevision(productId: string): Promise<CurrentRevisionResponse | null> {
    try {
      return await this.http.get<CurrentRevisionResponse>(`/products/${productId}/bom-revisions/current`);
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) return null;
      throw error;
    }
  }

  listRevisionLines(revisionId: string, params: PageParams = {}) {
    return this.http.get<PagedResponse<BomLine>>(`/bom-revisions/${revisionId}/lines`, params);
  }
}

/** Fetches every line of a revision, following the standard pager. */
export async function listAllRevisionLines(api: ProductsApi, revisionId: string): Promise<BomLine[]> {
  const pageSize = 200;
  const first = await api.listRevisionLines(revisionId, { page: 1, pageSize });
  const lines = [...first.items];

  for (let page = 2; page <= Math.ceil(first.total / pageSize); page += 1) {
    const next = await api.listRevisionLines(revisionId, { page, pageSize });
    lines.push(...next.items);
  }

  return lines;
}
