import { describe, expect, it, vi } from 'vitest';
import { ApiError } from './http';
import type { HttpClient } from './http';
import { LiveProductsApi, listAllRevisionLines } from './productsApi';
import type { ProductsApi } from './productsApi';
import type {
  BomLine,
  CurrentRevisionResponse,
} from './types';

const id = '11111111-1111-1111-1111-111111111111';

function fakeHttp() {
  const get = vi.fn().mockResolvedValue({});
  return { client: { get } as unknown as HttpClient, get };
}

describe('LiveProductsApi', () => {
  it('lists products with the supplied filters', async () => {
    const { client, get } = fakeHttp();
    const params = { search: 'frame', status: 'Active' as const, page: 2, pageSize: 10 };
    await new LiveProductsApi(client).listProducts(params);

    expect(get).toHaveBeenCalledWith('/products', params);
  });

  it('reads a single product', async () => {
    const { client, get } = fakeHttp();
    await new LiveProductsApi(client).getProduct(id);

    expect(get).toHaveBeenCalledWith(`/products/${id}`);
  });

  it('lists components with the supplied filters', async () => {
    const { client, get } = fakeHttp();
    const params = { search: 'bracket', status: 'Active' as const };
    await new LiveProductsApi(client).listComponents(params);

    expect(get).toHaveBeenCalledWith('/components', params);
  });

  it('reads a single component', async () => {
    const { client, get } = fakeHttp();
    await new LiveProductsApi(client).getComponent(id);

    expect(get).toHaveBeenCalledWith(`/components/${id}`);
  });

  it('lists BOM revisions for a product', async () => {
    const { client, get } = fakeHttp();
    await new LiveProductsApi(client).listBomRevisions(id, { pageSize: 200 });

    expect(get).toHaveBeenCalledWith(`/products/${id}/bom-revisions`, { pageSize: 200 });
  });

  it('reads a single BOM revision', async () => {
    const { client, get } = fakeHttp();
    await new LiveProductsApi(client).getBomRevision(id);

    expect(get).toHaveBeenCalledWith(`/bom-revisions/${id}`);
  });

  it('treats a missing current revision as an empty configuration', async () => {
    const { client, get } = fakeHttp();
    get.mockRejectedValue(new ApiError(404, { title: 'Not found' }));

    await expect(new LiveProductsApi(client).getCurrentRevision(id)).resolves.toBeNull();
  });

  it('keeps a non-404 current-revision failure as an error', async () => {
    const { client, get } = fakeHttp();
    const failure = new ApiError(500, { title: 'Server error' });
    get.mockRejectedValue(failure);

    await expect(new LiveProductsApi(client).getCurrentRevision(id)).rejects.toBe(failure);
  });

  it('returns the current revision when it exists', async () => {
    const { client, get } = fakeHttp();
    const response = { revision: { id: 'revision-1' }, lines: [] } as unknown as CurrentRevisionResponse;
    get.mockResolvedValue(response);

    await expect(new LiveProductsApi(client).getCurrentRevision(id)).resolves.toBe(response);
  });

  it('lists revision lines with paging parameters', async () => {
    const { client, get } = fakeHttp();
    await new LiveProductsApi(client).listRevisionLines(id, { page: 2, pageSize: 200 });

    expect(get).toHaveBeenCalledWith(`/bom-revisions/${id}/lines`, { page: 2, pageSize: 200 });
  });
});

describe('listAllRevisionLines', () => {
  it('follows the standard pager and concatenates every page', async () => {
    const firstLine = { id: 'line-1' } as BomLine;
    const secondLine = { id: 'line-2' } as BomLine;
    const listRevisionLines = vi
      .fn()
      .mockResolvedValueOnce({ items: [firstLine], total: 201, page: 1, pageSize: 200 })
      .mockResolvedValueOnce({ items: [secondLine], total: 201, page: 2, pageSize: 200 });
    const api = { listRevisionLines } as unknown as ProductsApi;

    await expect(listAllRevisionLines(api, id)).resolves.toEqual([firstLine, secondLine]);
    expect(listRevisionLines).toHaveBeenNthCalledWith(1, id, { page: 1, pageSize: 200 });
    expect(listRevisionLines).toHaveBeenNthCalledWith(2, id, { page: 2, pageSize: 200 });
  });

  it('does not request a second page when the first page is complete', async () => {
    const line = { id: 'line-1' } as BomLine;
    const listRevisionLines = vi.fn().mockResolvedValue({
      items: [line],
      total: 1,
      page: 1,
      pageSize: 200,
    });
    const api = { listRevisionLines } as unknown as ProductsApi;

    await expect(listAllRevisionLines(api, id)).resolves.toEqual([line]);
    expect(listRevisionLines).toHaveBeenCalledTimes(1);
  });
});
