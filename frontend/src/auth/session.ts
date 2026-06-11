// Minimal client-side session: the JWT lives in localStorage. Mutations dispatch an
// 'auth-change' event so the header (and anything else) can re-render on login/logout.

const TOKEN_KEY = 'token';
export const AUTH_CHANGED = 'auth-change';

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function isAuthenticated(): boolean {
  return !!getToken();
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token);
  window.dispatchEvent(new Event(AUTH_CHANGED));
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY);
  window.dispatchEvent(new Event(AUTH_CHANGED));
}

// ── JWT claims (read-only; the Gateway is the real authority) ────────
// Decoding the payload is for UX gating only — every protected action is
// still enforced server-side. Never trust these values for security.
interface JwtClaims {
  sub?: string;
  role?: string;
  email?: string;
}

function decodeClaims(): JwtClaims | null {
  const token = getToken();
  const payload = token?.split('.')[1];
  if (!payload) return null;
  try {
    const b64 = payload.replace(/-/g, '+').replace(/_/g, '/');
    const padded = b64.padEnd(b64.length + ((4 - (b64.length % 4)) % 4), '=');
    return JSON.parse(atob(padded)) as JwtClaims;
  } catch {
    return null;
  }
}

export function getUserId(): string | null {
  return decodeClaims()?.sub ?? null;
}

export function getRole(): string | null {
  return decodeClaims()?.role ?? null;
}

export function isAdmin(): boolean {
  return getRole() === 'Admin';
}

/** Display name for the logged-in user — the email local-part (before @). Null when not logged in. */
export function getDisplayName(): string | null {
  const email = decodeClaims()?.email;
  return email ? email.split('@')[0] : null;
}
