import { useState } from 'react';
import api, { apiErrorMessage } from '../api/api';
import { setToken } from '../auth/session';
import type { AuthResponse } from '../types/poll.types';

export function useAuth() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const run = async (path: '/auth/login' | '/auth/register', email: string, password: string) => {
    setLoading(true);
    setError(null);
    try {
      const { data } = await api.post<AuthResponse>(path, { email, password });
      setToken(data.token);
      return true;
    } catch (err) {
      setError(apiErrorMessage(err, 'Authentication failed'));
      return false;
    } finally {
      setLoading(false);
    }
  };

  return {
    login: (email: string, password: string) => run('/auth/login', email, password),
    register: (email: string, password: string) => run('/auth/register', email, password),
    loading,
    error,
  };
}
