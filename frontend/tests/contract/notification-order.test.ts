import test from 'node:test';
import assert from 'node:assert/strict';
import { createNotificationState, reduceNotification } from '../../src/state/notification-reducer.ts';

test('notifications are hints and stale/unknown versions are rejected', () => {
  let state = createNotificationState();
  state = reduceNotification(state, { eventType: 'StateChanged', schemaVersion: 's01/notification/2.0', runId: 'r', revision: 2 });
  state = reduceNotification(state, { eventType: 'StateChanged', schemaVersion: 's01/notification/2.0', runId: 'r', revision: 1 });
  assert.equal(state.latestByRun.get('r')?.revision, 2); assert.equal(state.rejected, 1);
  state = reduceNotification(state, { eventType: 'StateChanged', schemaVersion: 'future', runId: 'r', revision: 3 });
  assert.equal(state.rejected, 2);
});


test('v2 summary is a finite hint and never an action or commit receipt', () => {
  const summary = { executionState: 'Detection', wholeTaskState: 'AwaitingFinalUnloadCompletion', errorCode: null };
  const state = reduceNotification(createNotificationState(), { eventType: 'StateChanged', schemaVersion: 's01/notification/2.0', runId: 'r', revision: 2, summary });
  assert.deepEqual(state.latestByRun.get('r')?.summary, summary);
  assert.deepEqual([...state.refreshRuns], ['r']);
  for (const version of ['s01/notification/1.0', 's01/notification/3.0']) {
    const rejected = reduceNotification(state, { eventType: 'StateChanged', schemaVersion: version, runId: 'r', revision: 3 });
    assert.equal(rejected.rejected, 1);
    assert.equal(rejected.latestByRun.get('r')?.revision, 2);
  }
  for (const invalid of ['Completed', { ...summary, allowedActions: ['Start'] }]) {
    const rejected = reduceNotification(state, { eventType: 'StateChanged', schemaVersion: 's01/notification/2.0', runId: 'r', revision: 3, summary: invalid as any });
    assert.equal(rejected.rejected, 1);
    assert.equal(rejected.latestByRun.get('r')?.revision, 2);
  }
});
