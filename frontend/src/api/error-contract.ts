export type ErrorContract = {
  code: string;
  message: string;
  category?: string;
  traceId?: string;
  retryable?: boolean;
  details?: unknown;
  currentRevision?: number | string;
  httpStatus?: number;
};

export class ApiError extends Error {
  readonly contract: ErrorContract;
  constructor(contract: ErrorContract) {
    super(contract.message || `HTTP ${contract.httpStatus ?? 0}`);
    this.name = 'ApiError';
    this.contract = contract;
  }
}

export async function parseErrorResponse(response: Response): Promise<ErrorContract> {
  let body: any = undefined;
  try { body = await response.clone().json(); } catch { /* non-JSON response */ }
  const code = body?.code ?? body?.error?.code ?? body?.error ?? `HTTP_${response.status}`;
  const message = body?.message ?? body?.error?.message ?? body?.detail ?? '后端请求失败，当前状态受限';
  return {
    code: String(code), message: String(message), category: body?.category,
    traceId: body?.traceId ?? body?.trace_id, retryable: body?.retryable,
    details: body?.details, currentRevision: body?.currentRevision, httpStatus: response.status
  };
}
