/**
 * Typed fetch wrapper.
 *
 * The API's error contract is RFC 7807 ProblemDetails plus a `code` extension on every
 * error and a `conflictType` extension on every 409 (contract "Error contract"). The UI
 * needs both machine-readable values, so they are first-class fields on ApiError rather
 * than being recovered from a rendered message.
 */

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  code?: string;
  conflictType?: string;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails;
  readonly code?: string;
  readonly conflictType?: string;

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? `Request failed with status ${status}`);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
    this.code = problem.code;
    this.conflictType = problem.conflictType;
  }

  /** The single value the UI shows: `code`, else `conflictType`, else an HTTP fallback. */
  get machineCode(): string {
    if (this.status === 0) return 'network-error';
    return this.code ?? this.conflictType ?? `http-${this.status}`;
  }

  get isNetworkError(): boolean {
    return this.status === 0;
  }
}

export function problemError(
  status: number,
  codes: { code?: string; conflictType?: string },
  title: string,
  detail?: string,
): ApiError {
  return new ApiError(status, {
    title,
    status,
    detail,
    code: codes.code,
    conflictType: codes.conflictType,
  });
}

type QueryValue = string | number | boolean | null | undefined;

/** Omits empty values so `?status=` never reaches a validator that rejects it. */
export function buildQuery(params: object = {}): string {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params) as Array<[string, QueryValue]>) {
    if (value === undefined || value === null || value === '') continue;
    query.set(key, String(value));
  }
  const text = query.toString();
  return text ? `?${text}` : '';
}

function toProblem(status: number, body: unknown): ProblemDetails {
  if (body && typeof body === 'object') {
    const candidate = body as ProblemDetails;
    return {
      ...candidate,
      status,
      title: candidate.title ?? `Request failed with status ${status}`,
    };
  }

  if (typeof body === 'string' && body.trim()) {
    return { status, title: body.trim() };
  }

  return { status, title: `Request failed with status ${status}` };
}

export class HttpClient {
  private readonly baseUrl: string;

  constructor(baseUrl = '/api') {
    this.baseUrl = baseUrl.replace(/\/$/, '');
  }

  async request<T>(path: string, init?: RequestInit): Promise<T> {
    const headers = new Headers(init?.headers);
    if (init?.body !== undefined && !headers.has('Content-Type')) {
      headers.set('Content-Type', 'application/json');
    }

    let response: Response;
    try {
      response = await fetch(`${this.baseUrl}${path}`, { ...init, headers });
    } catch {
      throw new ApiError(0, {
        title: 'Cannot reach the API',
        detail: 'The Forge PLM API did not respond. Check that the backend is running on port 5199.',
        code: 'network-error',
      });
    }

    if (response.status === 204) {
      return undefined as T;
    }

    const text = await response.text();
    let body: unknown = undefined;
    if (text) {
      try {
        body = JSON.parse(text);
      } catch {
        body = text;
      }
    }

    if (!response.ok) {
      throw new ApiError(response.status, toProblem(response.status, body));
    }

    if (body === undefined) {
      return undefined as T;
    }

    return body as T;
  }

  get<T>(path: string, params: object = {}): Promise<T> {
    return this.request<T>(`${path}${buildQuery(params)}`);
  }

  post<T>(path: string, body: unknown): Promise<T> {
    return this.request<T>(path, { method: 'POST', body: JSON.stringify(body) });
  }

  put<T>(path: string, body: unknown): Promise<T> {
    return this.request<T>(path, { method: 'PUT', body: JSON.stringify(body) });
  }
}

export function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    return error.message || error.problem.title || error.machineCode;
  }
  if (error instanceof Error) {
    return error.message;
  }
  return 'Unexpected error.';
}
