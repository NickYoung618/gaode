export function notificationViewModel(state: any) { return { pendingRefresh: [...(state?.refreshRuns ?? [])], latest: [...(state?.latestByRun?.values?.() ?? [])] }; }
