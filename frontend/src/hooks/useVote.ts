import { useState } from 'react';
import api, { apiErrorMessage } from '../api/api';
import type { VoteResults } from '../types/poll.types';

// One persistent voter token per browser (no login required for voters).
function getVoterToken(): string {
  let token = localStorage.getItem('voter_token');
  if (!token) {
    token = crypto.randomUUID();
    localStorage.setItem('voter_token', token);
  }
  return token;
}

export function useVote(pollCode: string) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [hasVoted, setHasVoted] = useState(false);

  const vote = async (optionIndex: number): Promise<VoteResults | null> => {
    setLoading(true);
    setError(null);
    try {
      const { data } = await api.post<VoteResults>(`/polls/${pollCode}/vote`, {
        optionIndex,
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
