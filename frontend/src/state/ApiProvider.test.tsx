import { act, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fakeApi } from '../test/fixtures';
import { ApiProvider, useApi } from './ApiProvider';

const { resolveApi } = vi.hoisted(() => ({ resolveApi: vi.fn() }));

vi.mock('../api', () => ({ resolveApi }));

function Probe() {
  const api = useApi();
  return <div>API ready for {String(api.products.listProducts.name)}</div>;
}

beforeEach(() => {
  resolveApi.mockReset();
});

describe('ApiProvider', () => {
  it('shows a connecting state until the API bundle resolves', async () => {
    let resolveBundle!: (value: ReturnType<typeof fakeApi>) => void;
    resolveApi.mockReturnValue(
      new Promise((resolve) => {
        resolveBundle = resolve;
      }),
    );

    render(
      <ApiProvider>
        <Probe />
      </ApiProvider>,
    );

    expect(screen.getByRole('status')).toHaveTextContent('Connecting to the Forge PLM API');

    await act(async () => {
      resolveBundle(fakeApi());
      await Promise.resolve();
    });

    expect(await screen.findByText(/API ready for/)).toBeInTheDocument();
  });

  it('renders the startup error instead of the app', async () => {
    resolveApi.mockRejectedValue(new Error('Cannot reach the API'));

    render(
      <ApiProvider>
        <Probe />
      </ApiProvider>,
    );

    expect(await screen.findByRole('heading', { name: 'Cannot start Forge PLM' })).toBeInTheDocument();
    expect(screen.getByText('Cannot reach the API')).toBeInTheDocument();
  });

  it('uses a stable message when the rejection is not an Error', async () => {
    resolveApi.mockRejectedValue('not an error');

    render(
      <ApiProvider>
        <Probe />
      </ApiProvider>,
    );

    expect(await screen.findByText('Could not initialise the API client.')).toBeInTheDocument();
  });

  it('rejects use outside the provider', () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);

    expect(() => render(<Probe />)).toThrow('useApi must be used inside ApiProvider.');
  });
});
