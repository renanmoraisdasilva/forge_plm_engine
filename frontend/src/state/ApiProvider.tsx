import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { resolveApi, type ApiBundle } from '../api';

const ApiContext = createContext<ApiBundle | null>(null);

export function ApiProvider({ children }: { children: ReactNode }) {
  const [bundle, setBundle] = useState<ApiBundle | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    resolveApi()
      .then((resolved) => {
        if (!cancelled) setBundle(resolved);
      })
      .catch((reason: unknown) => {
        if (!cancelled) setError(reason instanceof Error ? reason.message : 'Could not initialise the API client.');
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const value = useMemo(() => bundle, [bundle]);

  if (error) {
    return (
      <div className="flex h-full items-center justify-center p-8">
        <div className="max-w-md rounded border border-danger-line bg-surface p-6">
          <h1 className="text-[15px] font-semibold">Cannot start Forge PLM</h1>
          <p className="mt-2 text-[13px] text-ink-subtle">{error}</p>
        </div>
      </div>
    );
  }

  if (!value) {
    return (
      <div className="flex h-full items-center justify-center p-8" role="status">
        <p className="text-[13px] text-ink-muted">{'Connecting to the Forge PLM API\u2026'}</p>
      </div>
    );
  }

  return <ApiContext.Provider value={value}>{children}</ApiContext.Provider>;
}

export function useApi(): ApiBundle {
  const value = useContext(ApiContext);
  if (!value) {
    throw new Error('useApi must be used inside ApiProvider.');
  }
  return value;
}
