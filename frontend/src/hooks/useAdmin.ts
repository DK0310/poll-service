import { useCallback, useEffect, useState } from 'react';
import api, { apiErrorMessage } from '../api/api';
import type { AdminUser, PollInfo } from '../types/poll.types';

/**
 * Admin dashboard data + actions. Loads all polls and all users (admin-only endpoints),
 * and exposes moderation actions that re-fetch on success. The Gateway's `admin` policy
 * and each service's role re-check are the real authority — this is just the UI.
 */
export function useAdmin() {
  const [polls, setPolls] = useState<PollInfo[]>([]);
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let cancelled = false;
    Promise.all([api.get<PollInfo[]>('/admin/polls'), api.get<AdminUser[]>('/admin/users')])
      .then(([p, u]) => {
        if (!cancelled) {
          setPolls(p.data);
          setUsers(u.data);
          setError(null);
        }
      })
      .catch((err) => {
        if (!cancelled) setError(apiErrorMessage(err, 'Failed to load admin data'));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [reloadKey]);

  const refresh = useCallback(() => setReloadKey((k) => k + 1), []);

  const action = useCallback(
    async (fn: () => Promise<unknown>) => {
      setBusy(true);
      setError(null);
      try {
        await fn();
        setReloadKey((k) => k + 1);
      } catch (err) {
        setError(apiErrorMessage(err, 'Action failed'));
      } finally {
        setBusy(false);
      }
    },
    [],
  );

  const closePoll = (code: string) => action(() => api.patch(`/polls/${code}/close`));
  const deletePoll = (code: string) => action(() => api.delete(`/polls/${code}`));
  const setRole = (id: string, role: 'User' | 'Admin') =>
    action(() => api.post(`/admin/users/${id}/role`, { role }));
  const deleteUser = (id: string) => action(() => api.delete(`/admin/users/${id}`));

  return { polls, users, loading, error, busy, refresh, closePoll, deletePoll, setRole, deleteUser };
}
