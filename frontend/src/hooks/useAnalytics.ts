import { useEffect, useState } from 'react';
import axios from 'axios';
import api from '../api/api';
import type { Analytics } from '../types/poll.types';

export function useAnalytics(pollCode: string) {
  const [analytics, setAnalytics] = useState<Analytics | null>(null);
  const [loading, setLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);

  useEffect(() => {
    let cancelled = false;

    api
      .get<Analytics>(`/polls/${pollCode}/analytics`)
      .then((r) => {
        if (!cancelled) setAnalytics(r.data);
      })
      .catch((e) => {
        if (!cancelled && axios.isAxiosError(e) && e.response?.status === 404) {
          setNotFound(true);
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [pollCode]);

  return { analytics, loading, notFound };
}
