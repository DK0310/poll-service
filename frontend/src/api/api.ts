import axios from 'axios';
import { clearToken, getToken } from '../auth/session';

// Single Axios instance — all REST calls go through the API Gateway.
// The frontend never talks to an individual microservice directly.
const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? 'http://localhost:5000/api',
});

// Attach the JWT if present.
api.interceptors.request.use((cfg) => {
  const token = getToken();
  if (token) {
    cfg.headers.Authorization = `Bearer ${token}`;
  }
  return cfg;
});

// Global 401 handling: an expired/invalid token → clear it and bounce to /login.
// (Login/register failures return 400, so they don't trip this.)
api.interceptors.response.use(
  (r) => r,
  (error) => {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      clearToken();
      if (window.location.pathname !== '/login') {
        window.location.assign('/login');
      }
    }
    return Promise.reject(error);
  },
);

/** Extracts a user-friendly error message from an Axios error. */
export function apiErrorMessage(err: unknown, fallback: string): string {
  if (axios.isAxiosError(err)) {
    const data = err.response?.data as { error?: string } | undefined;
    return data?.error ?? fallback;
  }
  return fallback;
}

export default api;
