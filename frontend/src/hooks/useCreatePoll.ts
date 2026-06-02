import { useState } from 'react';
import api, { apiErrorMessage } from '../api/api';
import type { CreatePollData, PollInfo } from '../types/poll.types';

export function useCreatePoll() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const createPoll = async (data: CreatePollData): Promise<PollInfo | null> => {
    setLoading(true);
    setError(null);
    try {
      const { data: poll } = await api.post<PollInfo>('/polls', data);
      return poll;
    } catch (err) {
      setError(apiErrorMessage(err, 'Failed to create poll'));
      return null;
    } finally {
      setLoading(false);
    }
  };

  return { createPoll, loading, error };
}
