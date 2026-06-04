import { useState } from 'react';
import api, { apiErrorMessage } from '../api/api';
import { getVoterToken } from '../auth/voter';
import type { VoteResults } from '../types/poll.types';

export function useVote(pollCode: string) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [hasVoted, setHasVoted] = useState(false);

  const vote = async (optionIndex: number, textAnswer?: string): Promise<VoteResults | null> => {
    setLoading(true);
    setError(null);
    try {
      const { data } = await api.post<VoteResults>(`/polls/${pollCode}/vote`, {
        optionIndex,
        textAnswer,
        voterToken: getVoterToken(),
      });
      setHasVoted(true);
      return data;
    } catch (err) {
      const msg = apiErrorMessage(err, 'Failed to submit vote');
      if (msg.toLowerCase().includes('already voted')) setHasVoted(true);
      setError(msg);
      return null;
    } finally {
      setLoading(false);
    }
  };

  return { vote, loading, error, hasVoted };
}
