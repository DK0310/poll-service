import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Check, ArrowRight, BarChart3, LogIn } from 'lucide-react';
import { PollForm } from '../components/PollForm';
import { ShareLink } from '../components/ShareLink';
import { useCreatePoll } from '../hooks/useCreatePoll';
import { isAuthenticated } from '../auth/session';
import type { PollInfo, QuestionType } from '../types/poll.types';

export function CreatePollPage() {
  const { createPoll, loading, error } = useCreatePoll();
  const [result, setResult] = useState<PollInfo | null>(null);

  // Creating a poll requires an account (the API rejects anonymous creates with 401).
  if (!isAuthenticated()) {
    return (
      <div className="page">
        <div className="card empty-state">
          <h1>Create a poll</h1>
          <p className="muted">Log in to create a poll and manage it from your dashboard.</p>
          <Link to="/login" className="btn">
            <LogIn size={18} strokeWidth={2.25} aria-hidden="true" /> Log in to create
          </Link>
          <p className="muted" style={{ marginTop: 'var(--space-md)' }}>
            No account? <Link to="/register">Sign up</Link>
          </p>
        </div>
      </div>
    );
  }

  const handleSubmit = async (
    question: string,
    type: QuestionType,
    options: string[],
    expiryHours?: number,
  ) => {
    const poll = await createPoll({ question, type, options, expiryHours });
    if (poll) setResult(poll);
  };

  if (result) {
    return (
      <div className="page">
        <div className="card create-success">
          <span className="success-badge" aria-hidden="true">
            <Check size={26} strokeWidth={2.5} />
          </span>
          <h1 className="h-gradient">Poll created</h1>
          <p className="created-question">{result.question}</p>
          <ShareLink code={result.code} />
          <div className="created-links">
            <Link to={`/poll/${result.code}`} className="btn">
              Open voting page <ArrowRight size={18} strokeWidth={2.25} aria-hidden="true" />
            </Link>
            <Link to={`/poll/${result.code}/results`} className="btn-outline">
              <BarChart3 size={18} strokeWidth={2.25} aria-hidden="true" /> View live results
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="page">
      <div className="card">
        <h1>Create a poll</h1>
        <p className="muted page-intro">Ask anything — share a link and watch results update live.</p>
        <PollForm onSubmit={handleSubmit} disabled={loading} />
        {error && (
          <p className="error" role="alert">
            {error}
          </p>
        )}
      </div>
    </div>
  );
}
