import { act, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { Route, Routes } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import type { ApiBundle } from '../api';
import { ApiError } from '../api/http';
import type { ProductsApi } from '../api/productsApi';
import type { PagedResponse, Product } from '../api/types';
import { bomRevision, fakeApi, paged, product } from '../test/fixtures';
import { renderScreen } from '../test/renderScreen';
import { ProductsList } from './ProductsList';

const { apiState } = vi.hoisted(() => ({ apiState: { current: null as ApiBundle | null } }));

vi.mock('../state/ApiProvider', () => ({
  ApiProvider: ({ children }: { children: ReactNode }) => children,
  useApi: () => {
    if (!apiState.current) throw new Error('Test API bundle not set.');
    return apiState.current;
  },
}));

const frame = product({ id: 'product-1', partNumber: 'FP-100', name: 'Frame' });
const bracket = product({ id: 'product-2', partNumber: 'FP-200', name: 'Bracket' });
const revision = bomRevision({ revisionCode: 'B' });

function renderProducts() {
  return renderScreen(
    <Routes>
      <Route path="/products" element={<ProductsList />} />
      <Route path="/products/:productId" element={<div>Product detail route</div>} />
    </Routes>,
    { route: '/products' },
  );
}

function setApi(products: Partial<ProductsApi>) {
  apiState.current = fakeApi({ products });
}

describe('ProductsList', () => {
  it('shows the loading block until the first page resolves', async () => {
    let resolvePage!: (page: PagedResponse<Product>) => void;
    const listProducts = vi.fn(
      () =>
        new Promise<PagedResponse<Product>>((resolve) => {
          resolvePage = resolve;
        }),
    );
    setApi({ listProducts });

    renderProducts();

    expect(screen.getByRole('status')).toHaveTextContent('Loading products');

    await act(async () => {
      resolvePage(paged([frame]));
      await Promise.resolve();
    });

    expect(await screen.findByText('FP-100')).toBeInTheDocument();
  });

  it('renders current revisions, falls back to an em dash, and navigates on row click', async () => {
    const user = userEvent.setup();
    const listProducts = vi.fn().mockResolvedValue(paged([frame, bracket], { total: 2 }));
    const getCurrentRevision = vi.fn((id: string) =>
      id === 'product-1'
        ? Promise.resolve({ revision, lines: [] })
        : Promise.reject(new Error('No released revision')),
    );
    setApi({ listProducts, getCurrentRevision });

    renderProducts();

    expect(await screen.findByText('FP-100')).toBeInTheDocument();
    expect(screen.getByText('B')).toBeInTheDocument();

    const bracketRow = screen.getByText('FP-200').closest('tr');
    expect(bracketRow).not.toBeNull();
    expect(within(bracketRow as HTMLElement).getByText('\u2014')).toBeInTheDocument();

    await user.click(screen.getByText('FP-100'));
    expect(await screen.findByText('Product detail route')).toBeInTheDocument();
  });

  it('renders the error block and retries the request', async () => {
    const user = userEvent.setup();
    const listProducts = vi
      .fn()
      .mockRejectedValueOnce(new ApiError(500, { title: 'Server error' }))
      .mockResolvedValueOnce(paged([frame]));
    setApi({ listProducts });

    renderProducts();

    expect(await screen.findByText('Request failed')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByText('FP-100')).toBeInTheDocument();
    expect(listProducts).toHaveBeenCalledTimes(2);
  });

  it('distinguishes an empty catalogue from an empty filter result', async () => {
    const user = userEvent.setup();
    const listProducts = vi.fn().mockResolvedValue(paged<Product>([]));
    setApi({ listProducts });

    renderProducts();

    expect(await screen.findByText('No products yet')).toBeInTheDocument();

    await user.type(screen.getByRole('searchbox', { name: 'Search products' }), 'frame');
    await waitFor(
      () =>
        expect(listProducts).toHaveBeenLastCalledWith(
          expect.objectContaining({ search: 'frame', page: 1 }),
        ),
      { timeout: 1500 },
    );

    expect(await screen.findByText('No products match these filters')).toBeInTheDocument();
  });

  it('requests the next page through the standard pager', async () => {
    const user = userEvent.setup();
    const listProducts = vi
      .fn()
      .mockResolvedValue(paged([frame], { total: 120, page: 1, pageSize: 50 }));
    setApi({ listProducts });

    renderProducts();

    expect(await screen.findByText('Page 1 of 3')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Next' }));

    await waitFor(() =>
      expect(listProducts).toHaveBeenLastCalledWith(expect.objectContaining({ page: 2 })),
    );
  });
});
