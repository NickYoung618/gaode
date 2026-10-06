export const RESTRICTED_STATES = new Set(['NotIntegrated', 'NotReady', 'Unknown', 'Pending', 'CommitUnknown', 'HandoffNotReady']);
export function projectStatus(snapshot: any) { return snapshot ? structuredClone(snapshot) : undefined; }
export function isSuccess(value: unknown): boolean { return value === 'Success' || value === 'OK' || value === 'Completed'; }
export function displayState(value: unknown): string { return typeof value === 'string' ? value : 'Unknown'; }
export function canDisplaySuccess(value: unknown): boolean { return !RESTRICTED_STATES.has(String(value)) && isSuccess(value); }
