import { useEffect, useState } from 'react';
import axios from 'axios';
import api from '../api/api';
import type { PollInfo } from '../types/poll.types';

export function usePollInfo(code: string) {
  const [poll, setPoll] = useState<PollInfo | null>(null);
  const [loading, setLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);

  useEffect(() => {
    let cancelled = false;

    api
      .get<PollInfo>(`/polls/${code}`)
      .then((r) => {
        if (!cancelled) setPoll(r.data);
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
  }, [code]);

  return { poll, loading, notFound };
}
