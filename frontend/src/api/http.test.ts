import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, HttpClient, buildQuery } from './http';
import { mapEcoError } from '../lib/ecoErrors';

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('buildQuery', () => {
  it('omits empty, null and undefined values', () => {
    expect(buildQuery({ search: 'abc', status: '', page: 1, productId: null, x: undefined })).toBe(
      '?search=abc&page=1',
    );
  });

  it('returns an empty string when nothing is supplied', () => {
    expect(buildQuery({})).toBe('');
  });
});

describe('HttpClient', () => {
  it('parses a 400 ProblemDetails body into ApiError.code', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
          title: 'Validation failed.',
          status: 400,
          detail: 'A title is required.',
          code: 'blank-title',
        }),
        { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
      ),
    );
    vi.stubGlobal('fetch', fetchMock);

    const http = new HttpClient('/api');
    const error = await http.post('/ecos', { title: '' }).catch((reason: unknown) => reason);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({
      status: 400,
      code: 'blank-title',
      message: 'A title is required.',
    });
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/ecos',
      expect.objectContaining({ method: 'POST' }),
    );
  });

  it('surfaces conflictType on a 409 that carries no code', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ title: 'Invalid transition.', status: 409, conflictType: 'invalid-transition' }), {
          status: 409,
          headers: { 'Content-Type': 'application/problem+json' },
        }),
      ),
    );

    const error = await new HttpClient('/api').get('/ecos/1').catch((reason: unknown) => reason);
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).conflictType).toBe('invalid-transition');
    expect((error as ApiError).code).toBeUndefined();
  });

  it('carries the query string for list endpoints', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(JSON.stringify({ items: [], total: 0, page: 2, pageSize: 10 }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    vi.stubGlobal('fetch', fetchMock);

    const result = await new HttpClient('/api').get<{ total: number }>('/ecos', {
      search: '',
      status: 'Draft',
      page: 2,
      pageSize: 10,
    });

    expect(result.total).toBe(0);
    expect(fetchMock).toHaveBeenCalledWith('/api/ecos?status=Draft&page=2&pageSize=10', expect.anything());
  });

  it('reports a network failure as a status-0 ApiError', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('fetch failed')));

    const error = await new HttpClient('/api').get('/products').catch((reason: unknown) => reason);
    expect(error).toMatchObject({ status: 0, code: 'network-error' });
    expect((error as ApiError).isNetworkError).toBe(true);
  });

  it('keeps non-JSON error text instead of losing it', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(new Response('Bad gateway', { status: 502 })),
    );

    const error = await new HttpClient('/api').get('/products').catch((reason: unknown) => reason);
    expect(error).toMatchObject({ status: 502 });
    expect((error as ApiError).message).toBe('Bad gateway');
  });
});

describe('ApiError', () => {
  it('surfaces code and conflictType as distinct machine values', () => {
    const error = new ApiError(409, {
      title: 'Conflict',
      detail: 'Base revision is stale.',
      conflictType: 'stale-base-revision',
      code: 'stale-base-revision',
    });

    expect(error.status).toBe(409);
    expect(error.code).toBe('stale-base-revision');
    expect(error.conflictType).toBe('stale-base-revision');
    expect(error.machineCode).toBe('stale-base-revision');
    expect(error.message).toBe('Base revision is stale.');
  });

  it('falls back to a conflictType when a 409 has no code', () => {
    const error = new ApiError(409, { title: 'Conflict', conflictType: 'invalid-transition' });
    expect(error.code).toBeUndefined();
    expect(error.machineCode).toBe('invalid-transition');
  });

  it('treats a status of zero as a network failure', () => {
    const error = new ApiError(0, { title: 'Cannot reach the API', code: 'network-error' });
    expect(error.isNetworkError).toBe(true);
    expect(error.machineCode).toBe('network-error');
  });
});

describe('mapEcoError', () => {
  it('maps duplicate-target-component onto the lines section', () => {
    const mapped = mapEcoError(
      new ApiError(400, { title: 'Validation failed', code: 'duplicate-target-component' }),
    );
    expect(mapped.code).toBe('duplicate-target-component');
    expect(mapped.fields.lines).toMatch(/same component/);
    expect(mapped.form).toBeNull();
  });

  it('maps stale-base-revision to an actionable form message', () => {
    const mapped = mapEcoError(
      new ApiError(409, { title: 'Conflict', conflictType: 'stale-base-revision' }),
    );
    expect(mapped.code).toBe('stale-base-revision');
    expect(mapped.form).toMatch(/outdated BOM/);
  });

  it('keeps an unknown code visible instead of hiding it', () => {
    const mapped = mapEcoError(new ApiError(418, { title: 'Teapot', code: 'mystery-code' }));
    expect(mapped.form).toMatch(/mystery-code/);
  });
});
