import { HttpClient } from './http';
import { LiveProductsApi, type ProductsApi } from './productsApi';
import { LiveEcoApi, type EcoApi } from './ecoApi';
import { MockEcoApi } from './mockEcoApi';
import type { EcoSummary, PagedResponse } from './types';

export type EcoMode = 'live' | 'mock';

export interface ApiBundle {
  products: ProductsApi;
  eco: EcoApi;
  ecoMode: EcoMode;
}

/**
 * Resolves the ECO implementation once at startup.
 *
 * The frozen contract's ECO endpoints do not exist until Agent B lands Slices 4–6. A
 * single probe tells us which world we are in: if `GET /api/ecos` answers, the live
 * implementation is used; if it is a routing 404, the mock is used and the UI says so.
 * `VITE_ECO_API_MODE=live|mock` overrides the probe for development.
 */
export async function resolveApi(
  products: ProductsApi = new LiveProductsApi(new HttpClient()),
  http: HttpClient = new HttpClient(),
): Promise<ApiBundle> {
  const forced = import.meta.env.VITE_ECO_API_MODE;

  if (forced === 'live') {
    return { products, eco: new LiveEcoApi(http), ecoMode: 'live' };
  }
  if (forced === 'mock') {
    return { products, eco: new MockEcoApi(products), ecoMode: 'mock' };
  }

  try {
    // The list endpoint is the cheapest readiness signal for the whole ECO surface.
    await http.get<PagedResponse<EcoSummary>>('/ecos', { pageSize: 1 });
    return { products, eco: new LiveEcoApi(http), ecoMode: 'live' };
  } catch {
    console.info('[forge-plm] ECO endpoints are not live yet; using the contract mock adapter.');
    return { products, eco: new MockEcoApi(products), ecoMode: 'mock' };
  }
}
