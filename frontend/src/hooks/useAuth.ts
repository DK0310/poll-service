import { useState } from 'react';
import api, { apiErrorMessage } from '../api/api';
import { setToken } from '../auth/session';
import type { AuthResponse, GoogleAuthResponse, OtpPurpose } from '../types/poll.types';

export function useAuth() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Wraps a request with loading/error state; returns the payload on success, null on failure.
  async function call<T>(fn: () => Promise<T>, fallback: string): Promise<T | null> {
    setLoading(true);
    setError(null);
    try {
      return await fn();
    } catch (err) {
      setError(apiErrorMessage(err, fallback));
      return null;
    } finally {
      setLoading(false);
    }
  }

  return {
    // Register no longer logs in — the email must be verified first.
    register: (email: string, password: string) =>
      call(async () => {
        await api.post('/auth/register', { email, password });
        return true;
      }, 'Registration failed'),

    verifyEmail: (email: string, code: string) =>
      call(async () => {
        const { data } = await api.post<AuthResponse>('/auth/verify-email', { email, code });
        setToken(data.token);
        return true;
      }, 'Verification failed'),

    resendCode: (email: string, purpose: OtpPurpose) =>
      call(async () => {
        await api.post('/auth/resend-code', { email, purpose });
        return true;
      }, 'Could not resend the code'),

    login: (email: string, password: string) =>
      call(async () => {
        const { data } = await api.post<AuthResponse>('/auth/login', { email, password });
        setToken(data.token);
        return true;
      }, 'Login failed'),

    // Returns hasPassword so the caller can prompt a new Google user to set one.
    loginWithGoogle: (idToken: string) =>
      call(async () => {
        const { data } = await api.post<GoogleAuthResponse>('/auth/google', { idToken });
        setToken(data.token);
        return data;
      }, 'Google sign-in failed'),

    setPassword: (password: string) =>
      call(async () => {
        await api.post('/auth/set-password', { password });
        return true;
      }, 'Could not set the password'),

    forgotPassword: (email: string) =>
      call(async () => {
        await api.post('/auth/forgot-password', { email });
        return true;
      }, 'Request failed'),

    resetPassword: (email: string, code: string, newPassword: string) =>
      call(async () => {
        await api.post('/auth/reset-password', { email, code, newPassword });
        return true;
      }, 'Reset failed'),

    loading,
    error,
  };
}
