import type { AuthSession } from '../state/auth-session.ts';
export function loginViewModel(session: AuthSession) { return { state: session.state, subject: session.subject ?? '', canSubmit: session.state === 'Anonymous' || session.state === 'Rejected' }; }
