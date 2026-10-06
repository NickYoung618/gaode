import type { Station01Api, CommandRequest } from '../api/station01-api.ts';
export const commandId = () => crypto.randomUUID();
export function pause(api: Station01Api, runId: string, expectedRevision: number) { return api.pause(runId, { requestId: commandId(), expectedRevision }); }
export function cancel(api: Station01Api, runId: string, expectedRevision: number) { return api.cancel(runId, { requestId: commandId(), expectedRevision }); }
export function recoveryCheck(api: Station01Api, runId: string, body: CommandRequest) { return api.recoveryCheck(runId, body); }
export function continueRun(api: Station01Api, runId: string, body: CommandRequest) { return api.continueRun(runId, body); }
