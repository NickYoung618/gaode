export type UIState = 'Loading'|'Ready'|'Refreshing'|'Offline'|'Unauthorized'|'Forbidden'|'NotIntegrated'|'Unknown'|'Conflict'|'Error'|'HostUnavailable'|'PendingCommand';
export function transition(current: UIState, event: 'loaded'|'refresh'|'offline'|'unauthorized'|'forbidden'|'restricted'|'conflict'|'error'|'host'|'command'|'success'): UIState {
  if (event === 'loaded' || event === 'success') return 'Ready';
  if (event === 'refresh') return 'Refreshing';
  if (event === 'offline') return 'Offline';
  if (event === 'unauthorized') return 'Unauthorized';
  if (event === 'forbidden') return 'Forbidden';
  if (event === 'restricted') return 'Unknown';
  if (event === 'conflict') return 'Conflict';
  if (event === 'host') return 'HostUnavailable';
  if (event === 'command') return 'PendingCommand';
  return 'Error';
}
