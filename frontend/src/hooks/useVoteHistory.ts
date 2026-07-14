import { useEffect, useState } from 'react';
import api, { apiErrorMessage } from '../api/api';
import type { VoteHistoryItem } from '../types/poll.types';

// The polls the logged-in user has voted on (most recent first).
export function useVoteHistory() {
  const [items, setItems] = useState<VoteHistoryItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  // State is only set inside the async callbacks (never synchronously in the effect body).
  useEffect(() => {
    let cancelled = false;
    api
      .get<VoteHistoryItem[]>('/me/votes')
      .then((r) => {
        if (!cancelled) setItems(r.data);
      })
      .catch((err) => {
        if (!cancelled) {
          setError(apiErrorMessage(err, 'Could not load your vote history'));
          setItems([]);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  return { items, error };
}
