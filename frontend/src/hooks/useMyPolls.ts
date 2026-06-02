import { useCallback, useEffect, useState } from 'react';
import axios from 'axios';
import api, { apiErrorMessage } from '../api/api';
import type { PollInfo } from '../types/poll.types';

export function useMyPolls() {
  const [polls, setPolls] = useState<PollInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  // State is only set inside the async callbacks (never synchronously in the effect body).
  useEffect(() => {
    let cancelled = false;
    api
      .get<PollInfo[]>('/polls/my-polls')
      .then((r) => {
        if (!cancelled) {
          setPolls(r.data);
          setError(null);
        }
      })
      .catch((err) => {
        if (!cancelled && !(axios.isAxiosError(err) && err.response?.status === 401)) {
          setError(apiErrorMessage(err, 'Failed to load your polls'));
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [reloadKey]);

  // Triggers the effect to re-run (used after close/delete).
  const refresh = useCallback(() => setReloadKey((k) => k + 1), []);

  return { polls, loading, error, refresh };
}
