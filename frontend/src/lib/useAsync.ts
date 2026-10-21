import { useCallback, useEffect, useRef, useState, type DependencyList } from 'react';

export interface AsyncResult<T> {
  data: T | null;
  error: unknown;
  loading: boolean;
  reload: () => void;
}

/**
 * Small data-loading hook: no cache, no global store, one request per dependency change.
 * `loader` is read through a ref so callers can pass an inline closure without causing a
 * fetch loop; the explicit `deps` array is what re-triggers the request.
 */
export function useAsync<T>(loader: () => Promise<T>, deps: DependencyList): AsyncResult<T> {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [loading, setLoading] = useState(true);
  const [tick, setTick] = useState(0);
  const loaderRef = useRef(loader);
  loaderRef.current = loader;

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    loaderRef.current().then(
      (result) => {
        if (cancelled) return;
        setData(result);
        setLoading(false);
      },
      (reason: unknown) => {
        if (cancelled) return;
        setError(reason);
        setLoading(false);
      },
    );

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, tick]);

  const reload = useCallback(() => setTick((value) => value + 1), []);

  return { data, error, loading, reload };
}
