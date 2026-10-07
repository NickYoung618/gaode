export const PROTOTYPE_SHA256 = '3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0';

export type RuntimeMode = 'Test' | 'Simulation' | 'Production';
export type RuntimeConfig = {
  apiBaseUrl: string;
  signalrUrl: string;
  mode: RuntimeMode;
  resourceVersion: string;
  prototypeSha256: string;
};

function safeUrl(value: unknown, field: string): string {
  if (typeof value !== 'string' || !value) throw new Error(`${field} is required`);
  const url = new URL(value);
  if (!['http:', 'https:'].includes(url.protocol)) throw new Error(`${field} must use HTTP(S)`);
  if (/[\\]|(^|:)\/\/(localhost|127\.0\.0\.1)(:|\/)/i.test(value) && (globalThis as any).process?.env?.NODE_ENV === 'production') {
    throw new Error(`${field} cannot target a local host in production`);
  }
  return url.toString().replace(/\/$/, '');
}

export function createRuntimeConfig(raw: Partial<RuntimeConfig>): RuntimeConfig {
  const mode = raw.mode ?? 'Test';
  if (!['Test', 'Simulation', 'Production'].includes(mode)) throw new Error('Unsupported runtime mode');
  if (raw.prototypeSha256 !== PROTOTYPE_SHA256) throw new Error('Prototype hash does not match approved baseline');
  return {
    apiBaseUrl: safeUrl(raw.apiBaseUrl, 'apiBaseUrl'),
    signalrUrl: safeUrl(raw.signalrUrl, 'signalrUrl'),
    mode,
    resourceVersion: raw.resourceVersion || 'dev',
    prototypeSha256: PROTOTYPE_SHA256
  };
}
