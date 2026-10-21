import { HttpClient } from './http';
import { LiveProductsApi, type ProductsApi } from './productsApi';
import { LiveEcoApi, type EcoApi } from './ecoApi';

export interface ApiBundle {
  products: ProductsApi;
  eco: EcoApi;
}

/**
 * Binds the screens to the live API. Both surfaces are served from the same origin as the
 * app in development (Vite proxies `/api`) and in the demo container, so the client always
 * uses relative URLs and no CORS configuration exists.
 */
export async function resolveApi(
  products: ProductsApi = new LiveProductsApi(new HttpClient()),
  http: HttpClient = new HttpClient(),
): Promise<ApiBundle> {
  return { products, eco: new LiveEcoApi(http) };
}
