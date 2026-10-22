import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { Route, Routes } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import type { ApiBundle } from '../api';
import { ApiError } from '../api/http';
import type { EcoApi } from '../api/ecoApi';
import { ecoSummary, fakeApi, paged } from '../test/fixtures';
import { renderScreen } from '../test/renderScreen';
import { EcoList } from './EcoList';

const { apiState } = vi.hoisted(() => ({ apiState: { current: null as ApiBundle | null } }));

vi.mock('../state/ApiProvider', () => ({
  ApiProvider: ({ children }: { children: ReactNode }) => children,
  useApi: () => {
    if (!apiState.current) throw new Error('Test API bundle not set.');
    return apiState.current;
  },
}));

const replaceBracket = ecoSummary({
  id: 'eco-1',
  ecoNumber: 'ECO-0001',
  title: 'Replace the bracket',
  status: 'Approved',
});
const changeQuantity = ecoSummary({
  id: 'eco-2',
  ecoNumber: 'ECO-0002',
  title: 'Change the quantity',
  status: 'Draft',
});

function setApi(eco: Partial<EcoApi>) {
  apiState.current = fakeApi({ eco });
}

function renderEcos() {
  return renderScreen(
    <Routes>
      <Route path="/ecos" element={<EcoList />} />
      <Route path="/ecos/new" element={<div>Create ECO route</div>} />
      <Route path="/ecos/:ecoId" element={<div>ECO detail route</div>} />
    </Routes>,
    { route: '/ecos' },
  );
}

describe('EcoList', () => {
  it('renders the ECO table and passes status filters to the API', async () => {
    const user = userEvent.setup();
    const listEcos = vi.fn().mockResolvedValue(paged([replaceBracket, changeQuantity], { total: 2 }));
    setApi({ listEcos });

    renderEcos();

    expect(await screen.findByText('ECO-0001')).toBeInTheDocument();
    expect(screen.getByText('Replace the bracket')).toBeInTheDocument();
    expect(screen.getByText('ECO-0002')).toBeInTheDocument();

    await user.selectOptions(
      screen.getByRole('combobox', { name: 'Filter by ECO status' }),
      'Approved',
    );

    await waitFor(() =>
      expect(listEcos).toHaveBeenLastCalledWith(
        expect.objectContaining({ status: 'Approved', page: 1 }),
      ),
    );
  });

  it('navigates to the detail screen and the create screen', async () => {
    const user = userEvent.setup();
    setApi({
      listEcos: vi.fn().mockResolvedValue(paged([replaceBracket], { total: 1 })),
    });

    renderEcos();

    await user.click(await screen.findByText('ECO-0001'));
    expect(await screen.findByText('ECO detail route')).toBeInTheDocument();
  });

  it('navigates from the primary Create ECO action', async () => {
    const user = userEvent.setup();
    setApi({ listEcos: vi.fn().mockResolvedValue(paged([], { total: 0 })) });

    renderEcos();

    expect(await screen.findByText('No engineering changes yet')).toBeInTheDocument();
    const createButtons = screen.getAllByRole('button', { name: 'Create ECO' });
    await user.click(createButtons[0]);

    expect(await screen.findByText('Create ECO route')).toBeInTheDocument();
  });

  it('renders the error block and retries the list request', async () => {
    const user = userEvent.setup();
    const listEcos = vi
      .fn()
      .mockRejectedValueOnce(new ApiError(500, { title: 'Server error' }))
      .mockResolvedValueOnce(paged([replaceBracket], { total: 1 }));
    setApi({ listEcos });

    renderEcos();

    expect(await screen.findByText('Request failed')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByText('ECO-0001')).toBeInTheDocument();
    expect(listEcos).toHaveBeenCalledTimes(2);
  });
});
