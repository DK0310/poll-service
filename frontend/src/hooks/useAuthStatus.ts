import { useEffect, useState } from 'react';
import { AUTH_CHANGED, isAuthenticated, isAdmin } from '../auth/session';

/**
 * Reactive auth state. Components that show/hide on login/logout should use this instead
 * of calling isAuthenticated()/isAdmin() directly — those are read once at render and
 * won't update when the token changes. This subscribes to the `auth-change` event so the
 * component re-renders the moment you log in or out.
 */
export function useAuthStatus() {
  const [authed, setAuthed] = useState(isAuthenticated());
  const [admin, setAdmin] = useState(isAdmin());

  useEffect(() => {
    const sync = () => {
      setAuthed(isAuthenticated());
      setAdmin(isAdmin());
    };
    window.addEventListener(AUTH_CHANGED, sync);
    return () => window.removeEventListener(AUTH_CHANGED, sync);
  }, []);

  return { authed, isAdmin: admin };
}
