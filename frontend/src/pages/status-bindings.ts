import { displayState } from '../state/status-projection.ts';
export function statusViewModel(snapshot: any) { return { host: displayState(snapshot?.host), plc: displayState(snapshot?.plc?.connection), camera: displayState(snapshot?.camera?.state), algorithm: displayState(snapshot?.algorithm?.state), quality: displayState(snapshot?.quality) }; }
