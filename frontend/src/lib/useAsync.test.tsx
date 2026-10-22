import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { useAsync } from './useAsync';

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });
  return { promise, resolve, reject };
}

describe('useAsync', () => {
  it('starts loading and exposes the resolved value', async () => {
    const loader = vi.fn().mockResolvedValue('loaded');
    const { result } = renderHook(() => useAsync(loader, []));

    expect(result.current.loading).toBe(true);

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.data).toBe('loaded');
    expect(result.current.error).toBeNull();
    expect(loader).toHaveBeenCalledTimes(1);
  });

  it('exposes a rejected request without losing the loading transition', async () => {
    const failure = new Error('API unavailable');
    const loader = vi.fn().mockRejectedValue(failure);
    const { result } = renderHook(() => useAsync(loader, []));

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.error).toBe(failure);
    expect(result.current.data).toBeNull();
  });

  it('reload runs the loader again and enters loading first', async () => {
    let call = 0;
    const loader = vi.fn(() => Promise.resolve((call += 1)));
    const { result } = renderHook(() => useAsync(loader, []));

    await waitFor(() => expect(result.current.data).toBe(1));

    act(() => result.current.reload());
    expect(result.current.loading).toBe(true);

    await waitFor(() => expect(result.current.data).toBe(2));
    expect(loader).toHaveBeenCalledTimes(2);
  });

  it('ignores a stale success when the dependencies change', async () => {
    const first = deferred<string>();
    const second = deferred<string>();
    const { result, rerender } = renderHook(
      ({ id }) => useAsync(() => (id === 1 ? first.promise : second.promise), [id]),
      { initialProps: { id: 1 } },
    );

    rerender({ id: 2 });
    await act(async () => {
      first.resolve('stale');
      await first.promise;
    });

    expect(result.current.data).toBeNull();
    expect(result.current.loading).toBe(true);

    await act(async () => {
      second.resolve('fresh');
      await second.promise;
    });

    await waitFor(() => expect(result.current.data).toBe('fresh'));
  });

  it('ignores a stale rejection when the dependencies change', async () => {
    const first = deferred<string>();
    const second = deferred<string>();
    const { result, rerender } = renderHook(
      ({ id }) => useAsync(() => (id === 1 ? first.promise : second.promise), [id]),
      { initialProps: { id: 1 } },
    );

    rerender({ id: 2 });
    await act(async () => {
      first.reject(new Error('stale failure'));
      await first.promise.catch(() => undefined);
    });

    expect(result.current.error).toBeNull();
    expect(result.current.loading).toBe(true);

    await act(async () => {
      second.resolve('fresh');
      await second.promise;
    });

    await waitFor(() => expect(result.current.data).toBe('fresh'));
  });
});
