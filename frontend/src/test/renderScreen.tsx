import { render } from '@testing-library/react';
import type { ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { ActorProvider } from '../state/ActorProvider';

/**
 * Screen tests own their route table, so each test can assert navigation targets.
 * The API context is mocked per test file; the actor provider is real so the
 * confirmation dialog sees the same demo identity the app uses.
 */
export function renderScreen(ui: ReactNode, { route = '/' }: { route?: string } = {}) {
  return render(
    <MemoryRouter initialEntries={[route]}>
      <ActorProvider>{ui}</ActorProvider>
    </MemoryRouter>,
  );
}
