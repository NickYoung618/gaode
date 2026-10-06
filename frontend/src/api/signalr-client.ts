import { reduceNotification, createNotificationState, type NotificationEnvelope } from '../state/notification-reducer.ts';
export class NotificationClient {
  state = createNotificationState();
  constructor(private readonly onRefresh: (runId: string) => void) {}
  receive(event: NotificationEnvelope) { const before = this.state.refreshRuns.size; this.state = reduceNotification(this.state, event); if (this.state.refreshRuns.size > before) this.onRefresh(event.runId ?? '__station__'); }
}
