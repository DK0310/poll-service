import { useCallback, useEffect, useState } from 'react';
import api, { apiErrorMessage } from '../api/api';
import type { Profile, UpdateProfile } from '../types/poll.types';

// Loads the current user's profile and exposes save / change-password actions.
export function useProfile() {
  const [profile, setProfile] = useState<Profile | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  // State is only set inside the async callbacks (never synchronously in the effect body).
  useEffect(() => {
    let cancelled = false;
    api
      .get<Profile>('/users/me')
      .then((r) => {
        if (!cancelled) {
          setProfile(r.data);
          setError(null);
        }
      })
      .catch((err) => {
        if (!cancelled) setError(apiErrorMessage(err, 'Could not load your profile'));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [reloadKey]);

  const reload = useCallback(() => setReloadKey((k) => k + 1), []);

  const save = async (update: UpdateProfile): Promise<boolean> => {
    setSaving(true);
    setError(null);
    try {
      const { data } = await api.put<Profile>('/users/me', update);
      setProfile(data);
      return true;
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not save your profile'));
      return false;
    } finally {
      setSaving(false);
    }
  };

  // Handles both first-time set (Google account, empty current) and a real change.
  const changePassword = async (currentPassword: string, newPassword: string): Promise<boolean> => {
    try {
      await api.post('/auth/change-password', { currentPassword, newPassword });
      reload(); // refresh hasPassword
      return true;
    } catch (err) {
      setError(apiErrorMessage(err, 'Could not update your password'));
      return false;
    }
  };

  return { profile, loading, saving, error, setError, save, changePassword, reload };
}
