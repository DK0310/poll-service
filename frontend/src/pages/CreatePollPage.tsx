import { useState } from 'react';
import { Link } from 'react-router-dom';
import { PollForm } from '../components/PollForm';
import { ShareLink } from '../components/ShareLink';
import { useCreatePoll } from '../hooks/useCreatePoll';
import type { PollInfo } from '../types/poll.types';

export function CreatePollPage() {
  const { createPoll, loading, error } = useCreatePoll();
  const [result, setResult] = useState<PollInfo | null>(null);

  const handleSubmit = async (question: string, options: string[], expiryHours?: number) => {
    const poll = await createPoll({ question, options, expiryHours });
    if (poll) setResult(poll);
  };

  return (
    <div className="page">
      <h1>Create a Poll</h1>
      {!result ? (
        <>
          <PollForm onSubmit={handleSubmit} disabled={loading} />
          {error && <p className="error">{error}</p>}
        </>
      ) : (
        <div className="success">
          <h2>Poll Created!</h2>
          <p className="created-question">{result.question}</p>
          <ShareLink code={result.code} />
          <div className="created-links">
            <Link to={`/poll/${result.code}`}>Open voting page →</Link>
            <Link to={`/poll/${result.code}/results`}>View live results →</Link>
          </div>
        </div>
      )}
    </div>
  );
}
