import { createContext, useContext, useMemo, useState, type ReactNode } from 'react';

/** D-06: demo-only identities. There is no authentication endpoint. */
export const ACTORS = ['Alex Morgan', 'Jordan Lee'] as const;
export type Actor = (typeof ACTORS)[number];

const STORAGE_KEY = 'forge-plm.actor';
const DEFAULT_ACTOR: Actor = 'Alex Morgan';

interface ActorContextValue {
  actor: Actor;
  setActor: (actor: Actor) => void;
}

const ActorContext = createContext<ActorContextValue | null>(null);

function readInitialActor(): Actor {
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY);
    if (stored === 'Alex Morgan' || stored === 'Jordan Lee') return stored;
  } catch {
    // Storage can be blocked; the demo still works with the default identity.
  }
  return DEFAULT_ACTOR;
}

export function ActorProvider({ children }: { children: ReactNode }) {
  const [actor, setActorState] = useState<Actor>(readInitialActor);

  const value = useMemo<ActorContextValue>(
    () => ({
      actor,
      setActor: (next: Actor) => {
        setActorState(next);
        try {
          window.localStorage.setItem(STORAGE_KEY, next);
        } catch {
          // Non-fatal: the selection simply does not survive a reload.
        }
      },
    }),
    [actor],
  );

  return <ActorContext.Provider value={value}>{children}</ActorContext.Provider>;
}

export function useActor(): ActorContextValue {
  const value = useContext(ActorContext);
  if (!value) {
    throw new Error('useActor must be used inside ActorProvider.');
  }
  return value;
}
