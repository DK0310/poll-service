import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Check, ArrowRight, BarChart3, LogIn } from 'lucide-react';
import { PollForm } from '../components/PollForm';
import { ShareLink } from '../components/ShareLink';
import { useCreatePoll } from '../hooks/useCreatePoll';
import { useAuthStatus } from '../hooks/useAuthStatus';
import { useToast } from '../components/Toast';
import type { PollInfo, QuestionType } from '../types/poll.types';

export function CreatePollPage() {
  const { createPoll, loading, error } = useCreatePoll();
  const { authed } = useAuthStatus();
  const { toast } = useToast();
  const [result, setResult] = useState<PollInfo | null>(null);

  // Creating a poll requires an account (the API rejects anonymous creates with 401).
  // useAuthStatus is reactive, so logging out here flips straight to the CTA.
  if (!authed) {
    return (
      <div className="board mx-auto w-full max-w-md">
        <div className="board-panel p-7 text-center sm:p-8">
          <h1 className="font-display text-2xl font-bold tracking-tight text-fg">Create a poll</h1>
          <p className="mt-2 text-fg-muted">Log in to create a poll and manage it from your dashboard.</p>
          <Link to="/login" className="board-btn mt-6">
            <LogIn size={18} strokeWidth={2.25} aria-hidden="true" /> Log in to create
          </Link>
          <p className="mt-4 text-sm text-fg-muted">
            No account?{' '}
            <Link to="/register" className="font-semibold text-tangerine hover:underline">
              Sign up
            </Link>
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
    if (poll) {
      setResult(poll);
      toast('Poll created');
    }
  };

  if (result) {
    return (
      <div className="board mx-auto w-full max-w-xl">
        <div className="board-panel p-7 text-center sm:p-9">
          <span
            className="mx-auto mb-4 grid h-14 w-14 place-items-center rounded-full bg-tangerine text-bg shadow-glow-tangerine"
            aria-hidden="true"
          >
            <Check size={26} strokeWidth={2.5} />
          </span>
          <h1 className="font-display text-2xl font-bold tracking-tight text-tangerine">Poll created</h1>
          <p className="mx-auto mb-6 mt-1 max-w-md font-display text-lg text-fg">{result.question}</p>
          <ShareLink code={result.code} />
          <div className="mt-6 flex flex-wrap justify-center gap-3">
            <Link to={`/poll/${result.code}`} className="board-btn">
              Open voting page <ArrowRight size={18} strokeWidth={2.25} aria-hidden="true" />
            </Link>
            <Link to={`/poll/${result.code}/results`} className="board-btn-outline">
              <BarChart3 size={18} strokeWidth={2.25} aria-hidden="true" /> View live results
            </Link>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="board mx-auto w-full max-w-xl">
      <div className="board-panel p-7 sm:p-9">
        <h1 className="font-display text-2xl font-bold tracking-tight text-fg">Create a poll</h1>
        <p className="mb-7 mt-1 text-fg-muted">Ask anything — share a link and watch results update live.</p>
        <PollForm onSubmit={handleSubmit} disabled={loading} />
        {error && (
          <p className="mt-4 text-sm text-tangerine" role="alert">
            {error}
          </p>
        )}
      </div>
    </div>
  );
}
