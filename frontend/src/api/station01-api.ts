import type { HttpClient } from './http-client.ts';

export type CommandRequest = { requestId: string; expectedRevision?: number; [key: string]: unknown };
export class Station01Api {
  private readonly http: HttpClient;
  constructor(http: HttpClient) { this.http = http; }
  status(etag?: string) { return this.http.get('/api/v1/station01/status', etag); }
  run(runId: string, etag?: string) { return this.http.get(`/api/v1/station01/runs/${encodeURIComponent(runId)}`, etag); }
  command(commandId: string) { return this.http.get(`/api/v1/station01/commands/${encodeURIComponent(commandId)}`); }
  handoff(runId: string) { return this.http.get(`/api/v1/station01/runs/${encodeURIComponent(runId)}/handoff`); }
  start(body: CommandRequest) { return this.http.post('/api/v1/station01/runs', body); }
  pause(runId: string, body: CommandRequest) { return this.http.post(`/api/v1/station01/runs/${encodeURIComponent(runId)}/pause`, body, body.expectedRevision); }
  cancel(runId: string, body: CommandRequest) { return this.http.post(`/api/v1/station01/runs/${encodeURIComponent(runId)}/cancel`, body, body.expectedRevision); }
  recoveryCheck(runId: string, body: CommandRequest) { return this.http.post(`/api/v1/station01/runs/${encodeURIComponent(runId)}/recovery-checks`, body, body.expectedRevision); }
  continueRun(runId: string, body: CommandRequest) { return this.http.post(`/api/v1/station01/runs/${encodeURIComponent(runId)}/continue`, body, body.expectedRevision); }
  validateConfig(body: unknown) { return this.http.post('/api/v1/station01/public-config/validate', body); }
  reset(body: CommandRequest) { return this.http.post('/api/v1/station01/reset', body); }
  media(mediaId: string, etag?: string) {
    if (!/^[0-9a-f-]{36}$/i.test(mediaId)) throw new Error('mediaId must be a GUID');
    return this.http.get(`/api/v1/station01/media/${mediaId}`, etag);
  }
}
