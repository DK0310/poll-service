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
