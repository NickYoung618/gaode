import type { Station01Api } from '../api/station01-api.ts';
export type StartState = { requestId: string; commandId?: string; runId?: string; status: 'Pending'|'Accepted'|'Unknown'|'Error' };
export function newRequestId(): string { return crypto.randomUUID(); }
export async function startRun(api: Station01Api, body: Record<string, unknown>, requestId = newRequestId()): Promise<StartState> {
  const receipt = await api.start({ ...body, requestId });
  const value: any = receipt.value;
  return { requestId, commandId: value?.commandId, runId: value?.runId, status: value?.requestAccepted ? 'Accepted' : 'Unknown' };
}
export async function recoverStart(api: Station01Api, state: StartState) {
  if (!state.commandId) return state;
  const result: any = await api.command(state.commandId);
  return { ...state, runId: result.value?.runId ?? state.runId, status: result.value?.terminalDecision ? 'Accepted' : 'Unknown' };
}
