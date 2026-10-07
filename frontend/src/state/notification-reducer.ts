export type NotificationSummary = { executionState: string; wholeTaskState: string; errorCode: string | null };
export type NotificationEnvelope = { eventType: string; schemaVersion: string; runId?: string; revision?: number; persistedRevision?: number; changedFields?: string[]; summary?: NotificationSummary | null };
export type NotificationState = { latestByRun: Map<string, NotificationEnvelope>; refreshRuns: Set<string>; rejected: number };
export function createNotificationState(): NotificationState { return { latestByRun: new Map(), refreshRuns: new Set(), rejected: 0 }; }
export function reduceNotification(state: NotificationState, event: NotificationEnvelope): NotificationState {
  const summary = event.summary;
  if (event.schemaVersion !== 's01/notification/2.0' || summary != null &&
      (typeof summary !== 'object' || typeof summary.executionState !== 'string' ||
       typeof summary.wholeTaskState !== 'string' ||
       !(summary.errorCode === null || typeof summary.errorCode === 'string') ||
       Object.keys(summary).some(key => !['executionState', 'wholeTaskState', 'errorCode'].includes(key))))
    return { ...state, rejected: state.rejected + 1 };
  const key = event.runId ?? '__station__';
  const old = state.latestByRun.get(key);
  const incoming = Number(event.revision ?? event.persistedRevision ?? 0);
  const previous = Number(old?.revision ?? old?.persistedRevision ?? -1);
  if (old && incoming <= previous) return { ...state, rejected: state.rejected + 1 };
  const latestByRun = new Map(state.latestByRun).set(key, event);
  const refreshRuns = new Set(state.refreshRuns).add(key);
  return { ...state, latestByRun, refreshRuns };
}
