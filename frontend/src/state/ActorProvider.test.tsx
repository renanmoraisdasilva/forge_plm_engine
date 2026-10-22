import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ActorProvider, useActor } from './ActorProvider';

function Probe() {
  const { actor, setActor } = useActor();
  return (
    <div>
      <span data-testid="actor">{actor}</span>
      <button type="button" onClick={() => setActor('Jordan Lee')}>
        Use Jordan
      </button>
    </div>
  );
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('ActorProvider', () => {
  it('defaults to Alex Morgan when nothing is stored', () => {
    render(
      <ActorProvider>
        <Probe />
      </ActorProvider>,
    );

    expect(screen.getByTestId('actor')).toHaveTextContent('Alex Morgan');
  });

  it('restores a valid stored actor', () => {
    window.localStorage.setItem('forge-plm.actor', 'Jordan Lee');

    render(
      <ActorProvider>
        <Probe />
      </ActorProvider>,
    );

    expect(screen.getByTestId('actor')).toHaveTextContent('Jordan Lee');
  });

  it('ignores an unrecognised stored value', () => {
    window.localStorage.setItem('forge-plm.actor', 'Mallory');

    render(
      <ActorProvider>
        <Probe />
      </ActorProvider>,
    );

    expect(screen.getByTestId('actor')).toHaveTextContent('Alex Morgan');
  });

  it('updates the actor and persists the choice', async () => {
    const user = userEvent.setup();
    render(
      <ActorProvider>
        <Probe />
      </ActorProvider>,
    );

    await user.click(screen.getByRole('button', { name: 'Use Jordan' }));

    expect(screen.getByTestId('actor')).toHaveTextContent('Jordan Lee');
    expect(window.localStorage.getItem('forge-plm.actor')).toBe('Jordan Lee');
  });

  it('still works when storage is blocked', async () => {
    const user = userEvent.setup();
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('storage blocked');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('storage blocked');
    });

    render(
      <ActorProvider>
        <Probe />
      </ActorProvider>,
    );

    expect(screen.getByTestId('actor')).toHaveTextContent('Alex Morgan');
    await user.click(screen.getByRole('button', { name: 'Use Jordan' }));
    expect(screen.getByTestId('actor')).toHaveTextContent('Jordan Lee');
  });

  it('rejects use outside the provider', () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);

    expect(() => render(<Probe />)).toThrow('useActor must be used inside ActorProvider.');
  });
});
