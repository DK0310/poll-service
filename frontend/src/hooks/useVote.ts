import { useState } from 'react';
import api, { apiErrorMessage } from '../api/api';
import { getVoterToken } from '../auth/voter';
import { getDisplayName, getRole } from '../auth/session';
import type { QuestionAnswer, VoteResults } from '../types/poll.types';

export function useVote(pollCode: string) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [hasVoted, setHasVoted] = useState(false);

  const vote = async (answers: QuestionAnswer[]): Promise<VoteResults | null> => {
    setLoading(true);
    setError(null);
    try {
      const { data } = await api.post<VoteResults>(`/polls/${pollCode}/vote`, {
        voterToken: getVoterToken(),
        // Display-only author label for OpenText answers (null = anonymous guest).
        authorName: getDisplayName(),
        authorRole: getRole(),
        answers,
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
