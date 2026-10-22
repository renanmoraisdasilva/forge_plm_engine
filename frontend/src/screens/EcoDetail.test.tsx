import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { Route, Routes } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import type { ApiBundle } from '../api';
import type { EcoApi } from '../api/ecoApi';
import { ApiError } from '../api/http';
import type { ProductsApi } from '../api/productsApi';
import type { AuditEvent, EcoDetail as EcoDetailDto, PagedResponse } from '../api/types';
import { auditEvent, bomLine, bomRevision, ecoDetail, fakeApi, paged } from '../test/fixtures';
import { renderScreen } from '../test/renderScreen';
import { EcoDetail } from './EcoDetail';

const { apiState } = vi.hoisted(() => ({ apiState: { current: null as ApiBundle | null } }));

vi.mock('../state/ApiProvider', () => ({
  ApiProvider: ({ children }: { children: ReactNode }) => children,
  useApi: () => {
    if (!apiState.current) throw new Error('Test API bundle not set.');
    return apiState.current;
  },
}));

function setApi({
  detail = ecoDetail(),
  eco = {},
  products = {},
  audits = paged<AuditEvent>([auditEvent()]),
}: {
  detail?: EcoDetailDto;
  eco?: Partial<EcoApi>;
  products?: Partial<ProductsApi>;
  audits?: PagedResponse<AuditEvent>;
} = {}) {
  apiState.current = fakeApi({
    eco: {
      getEco: vi.fn().mockResolvedValue(detail),
      listAudit: vi.fn().mockResolvedValue(audits),
      ...eco,
    },
    products: {
      listRevisionLines: vi.fn().mockResolvedValue(paged([bomLine()])),
      ...products,
    },
  });
}

function renderDetail() {
  return renderScreen(
    <Routes>
      <Route path="/ecos/:ecoId" element={<EcoDetail />} />
    </Routes>,
    { route: '/ecos/eco-1' },
  );
}

describe('EcoDetail', () => {
  it('gates Draft actions by the status matrix', async () => {
    setApi({
      detail: ecoDetail({ status: 'Draft', submittedAtUtc: null }),
    });

    renderDetail();

    expect(await screen.findByRole('heading', { name: 'Replace the bracket' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Edit draft' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Submit' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Reject' })).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Release' })).not.toBeInTheDocument();
  });

  it('approves with the current actor and optional comment', async () => {
    const user = userEvent.setup();
    window.localStorage.setItem('forge-plm.actor', 'Jordan Lee');
    const approveEco = vi.fn().mockResolvedValue(ecoDetail({ status: 'Approved' }));
    setApi({ eco: { approveEco } });

    renderDetail();

    expect(await screen.findByRole('heading', { name: 'Replace the bracket' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Submit' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Approve' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Reject' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'Release' })).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Approve' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('Jordan Lee')).toBeInTheDocument();

    await user.type(within(dialog).getByLabelText(/Review comment/), 'Looks good');
    await user.click(within(dialog).getByRole('button', { name: 'Approve ECO' }));

    await waitFor(() =>
      expect(approveEco).toHaveBeenCalledWith('eco-1', {
        actor: 'Jordan Lee',
        comment: 'Looks good',
      }),
    );
    expect(await screen.findByText(/ECO approved/)).toBeInTheDocument();
  });

  it('requires a rejection reason before enabling the destructive action', async () => {
    const user = userEvent.setup();
    const rejectEco = vi.fn().mockResolvedValue(ecoDetail({ status: 'Rejected' }));
    setApi({ eco: { rejectEco } });

    renderDetail();

    await screen.findByRole('heading', { name: 'Replace the bracket' });
    await user.click(screen.getByRole('button', { name: 'Reject' }));

    const dialog = await screen.findByRole('dialog');
    const confirm = within(dialog).getByRole('button', { name: 'Reject ECO' });
    expect(confirm).toBeDisabled();

    await user.type(within(dialog).getByLabelText(/Rejection reason/), 'Out of scope');
    expect(confirm).toBeEnabled();
    await user.click(confirm);

    await waitFor(() =>
      expect(rejectEco).toHaveBeenCalledWith('eco-1', {
        actor: 'Alex Morgan',
        rejectionReason: 'Out of scope',
      }),
    );
    expect(await screen.findByText(/ECO rejected/)).toBeInTheDocument();
  });

  it('keeps a mapped dialog error visible when an action fails', async () => {
    const user = userEvent.setup();
    const approveEco = vi
      .fn()
      .mockRejectedValue(new ApiError(400, { title: 'Validation failed', code: 'blank-actor' }));
    setApi({ eco: { approveEco } });

    renderDetail();

    await screen.findByRole('heading', { name: 'Replace the bracket' });
    await user.click(screen.getByRole('button', { name: 'Approve' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Approve ECO' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent(
      'Choose an actor in the header before making a change.',
    );
    expect(approveEco).toHaveBeenCalledTimes(1);
  });

  it('shows an actionable link when submit fails on a stale base revision', async () => {
    const user = userEvent.setup();
    const submitEco = vi
      .fn()
      .mockRejectedValue(
        new ApiError(409, { title: 'Conflict', conflictType: 'stale-base-revision' }),
      );
    setApi({
      detail: ecoDetail({ status: 'Draft', submittedAtUtc: null }),
      eco: { submitEco },
    });

    renderDetail();

    await screen.findByRole('heading', { name: 'Replace the bracket' });
    await user.click(screen.getByRole('button', { name: 'Submit' }));

    expect(await screen.findByText(/outdated BOM/)).toBeInTheDocument();
    expect(screen.getByText('stale-base-revision')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Create a new ECO' })).toHaveAttribute(
      'href',
      '/ecos/new?productId=product-1',
    );
  });

  it('still renders proposed lines when the base BOM cannot be loaded', async () => {
    setApi({
      products: {
        listRevisionLines: vi
          .fn()
          .mockRejectedValue(new ApiError(500, { title: 'Base BOM unavailable' })),
      },
    });

    renderDetail();

    expect(await screen.findByText(/base BOM lines could not be loaded/)).toBeInTheDocument();
    expect(screen.getByText('C-200')).toBeInTheDocument();
    expect(screen.getByText('Replace')).toBeInTheDocument();
  });

  it('asks for immediate effectivity when releasing a future-dated ECO', async () => {
    const user = userEvent.setup();
    const releaseEco = vi.fn().mockResolvedValue(ecoDetail({ status: 'Applied' }));
    setApi({
      detail: ecoDetail({
        status: 'Approved',
        requestedEffectiveAtUtc: '2999-01-01T00:00:00Z',
        approvedRevisionId: 'revision-1',
      }),
      eco: { releaseEco },
      products: {
        getBomRevision: vi.fn().mockResolvedValue(bomRevision({ status: 'Approved' })),
      },
    });

    renderDetail();

    await screen.findByRole('heading', { name: 'Replace the bracket' });
    await user.click(screen.getByRole('button', { name: 'Release' }));

    const dialog = await screen.findByRole('dialog');
    expect(
      within(dialog).getByText(/Scheduled effectivity is not supported in this prototype/),
    ).toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Release effective now' }));

    await waitFor(() =>
      expect(releaseEco).toHaveBeenCalledWith('eco-1', {
        actor: 'Alex Morgan',
        confirmImmediateEffectivity: true,
      }),
    );
    expect(await screen.findByText(/ECO applied/)).toBeInTheDocument();
  });

  it('renders the load error and retries the detail request', async () => {
    const user = userEvent.setup();
    const getEco = vi
      .fn()
      .mockRejectedValueOnce(new ApiError(500, { title: 'Server error' }))
      .mockResolvedValueOnce(ecoDetail());
    setApi({ eco: { getEco } });

    renderDetail();

    expect(await screen.findByText('Request failed')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByRole('heading', { name: 'Replace the bracket' })).toBeInTheDocument();
    expect(getEco).toHaveBeenCalledTimes(2);
  });
});
