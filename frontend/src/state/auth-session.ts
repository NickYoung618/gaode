export type AuthState = 'Anonymous'|'Authenticating'|'Authenticated'|'Rejected'|'Unavailable';
export type AuthSession = { state: AuthState; subject?: string; permissions: string[]; accessToken?: string };
export const unavailableSession = (): AuthSession => ({ state: 'Unavailable', permissions: [] });
export function authenticated(subject: string, permissions: string[], accessToken: string): AuthSession { return { state: 'Authenticated', subject, permissions: [...permissions], accessToken }; }
