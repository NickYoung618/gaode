import { ApiError, parseErrorResponse, type ErrorContract } from './error-contract.ts';

export type HttpClientOptions = { baseUrl: string; token?: string; fetchImpl?: typeof fetch };
export class HttpClient {
  private readonly baseUrl: string;
  private readonly token?: string;
  private readonly fetchImpl: typeof fetch;
  constructor(options: HttpClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/$/, ''); this.token = options.token; this.fetchImpl = options.fetchImpl ?? fetch;
  }
  async request<T>(path: string, init: RequestInit = {}): Promise<{ value: T; etag?: string; status: number }> {
    if (!path.startsWith('/')) throw new Error('API path must be relative to the configured backend');
    const headers = new Headers(init.headers);
    headers.set('Accept', 'application/json');
    if (this.token) headers.set('Authorization', `Bearer ${this.token}`);
    const response = await this.fetchImpl(`${this.baseUrl}${path}`, { ...init, headers });
    if (response.status === 304) return { value: undefined as T, etag: response.headers.get('ETag') ?? undefined, status: 304 };
    if (!response.ok) throw new ApiError(await parseErrorResponse(response));
    const etag = response.headers.get('ETag') ?? undefined;
    if (response.status === 204) return { value: undefined as T, etag, status: 204 };
    return { value: await response.json() as T, etag, status: response.status };
  }
  async get<T>(path: string, etag?: string) {
    const headers = etag ? { 'If-None-Match': etag } : undefined;
    return this.request<T>(path, { method: 'GET', headers });
  }
  post<T>(path: string, body: unknown, expectedRevision?: number) {
    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (expectedRevision !== undefined) headers['If-Match'] = String(expectedRevision);
    return this.request<T>(path, { method: 'POST', headers, body: JSON.stringify(body) });
  }
}

export function shouldRetryQuery(error: unknown): boolean {
  return error instanceof ApiError && ([429, 503].includes(error.contract.httpStatus ?? 0) || error.contract.retryable === true);
}
