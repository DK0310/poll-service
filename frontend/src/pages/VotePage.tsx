import { Link, useNavigate, useParams } from 'react-router-dom';
import { Lock, CheckCircle2, ArrowRight, ArrowLeft } from 'lucide-react';
import { usePollInfo } from '../hooks/usePollInfo';
import { useVote } from '../hooks/useVote';
import { SurveyForm } from '../components/SurveyForm';
import { AskPanel } from '../components/AskPanel';
import { ShareLink } from '../components/ShareLink';
import { getUserId, isAdmin } from '../auth/session';
import type { QuestionAnswer } from '../types/poll.types';

export function VotePage() {
  const { code = '' } = useParams<{ code: string }>();
  const navigate = useNavigate();
  const { poll, loading: pollLoading, notFound } = usePollInfo(code);
  const { vote, loading: voteLoading, error, hasVoted } = useVote(code);

  if (pollLoading) {
    return (
      <div className="board mx-auto w-full max-w-xl">
        <div className="board-panel p-7 sm:p-9">
          <p className="text-fg-muted">Loading…</p>
        </div>
      </div>
    );
  }

  if (notFound || !poll) {
    return (
      <div className="board mx-auto w-full max-w-xl">
        <div className="board-panel p-7 sm:p-9">
          <h1 className="font-display text-2xl font-bold tracking-tight text-fg">Poll not found</h1>
          <p className="mt-2 text-fg-muted">That poll code doesn’t exist or was removed.</p>
          <Link to="/create" className="board-btn mt-6">
            Create a poll
          </Link>
        </div>
      </div>
    );
  }

  const handleVote = async (answers: QuestionAnswer[]) => {
    const result = await vote(answers);
    if (result) navigate(`/poll/${code}/results`);
  };

  const canModerate = isAdmin() || (!!poll.creatorId && poll.creatorId === getUserId());

  return (
    <div className="board mx-auto w-full max-w-xl">
      <Link
        to="/"
        className="mb-4 inline-flex items-center gap-1.5 text-sm font-medium text-fg-muted transition-colors hover:text-fg"
      >
        <ArrowLeft size={18} strokeWidth={2.25} aria-hidden="true" /> Back to home
      </Link>

      <div className="board-panel board-grid p-7 sm:p-9">
        <h1 className="font-display text-2xl font-bold leading-tight tracking-tight text-fg text-balance sm:text-3xl">
          {poll.title ?? 'Cast your vote'}
        </h1>

        {!poll.isActive && (
          <p
            className="mt-4 flex items-center gap-2 rounded-lg border border-line bg-panel-2 px-4 py-3 text-sm text-fg-muted"
            role="status"
          >
            <Lock size={16} strokeWidth={2.25} aria-hidden="true" />
            This poll is closed.
          </p>
        )}

        {hasVoted ? (
          <div
            className="mt-4 flex items-center gap-2 rounded-lg border border-teal/40 bg-teal/10 px-4 py-3 text-sm text-teal"
            role="status"
          >
            <CheckCircle2 size={18} strokeWidth={2.25} aria-hidden="true" />
            <span>
              You’ve already voted on this poll.{' '}
              <Link to={`/poll/${code}/results`} className="inline-flex items-center gap-0.5 font-semibold underline">
                View live results <ArrowRight size={14} strokeWidth={2.25} aria-hidden="true" />
              </Link>
            </span>
          </div>
        ) : (
          <>
            <SurveyForm
              questions={poll.questions}
              onSubmit={handleVote}
              disabled={voteLoading || !poll.isActive}
              submitting={voteLoading}
            />
            {error && (
              <p className="mt-3 text-sm text-tangerine" role="alert">
                {error}
              </p>
            )}
          </>
        )}

        <div className="mt-8 border-t border-line pt-6">
          <p className="mb-2 text-sm text-fg-muted">Share this poll to collect votes:</p>
          <ShareLink code={code} />
        </div>
      </div>

      <AskPanel code={code} canModerate={canModerate} />
    </div>
  );
}
