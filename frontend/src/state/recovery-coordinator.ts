export type ReconnectOptions = { maxAttempts?: number; delayMs?: number; connect: () => Promise<void>; refresh: () => Promise<void>; onOffline: () => void };
export async function reconnect(options: ReconnectOptions): Promise<boolean> {
  const max = options.maxAttempts ?? 3; const delay = options.delayMs ?? 100;
  for (let attempt = 0; attempt < max; attempt++) { try { await options.connect(); await options.refresh(); return true; } catch { await new Promise(r => setTimeout(r, delay * (attempt + 1))); } }
  options.onOffline(); return false;
}
