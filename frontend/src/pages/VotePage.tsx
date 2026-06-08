import { Link, useNavigate, useParams } from 'react-router-dom';
import { Lock, CheckCircle2, ArrowRight } from 'lucide-react';
import { usePollInfo } from '../hooks/usePollInfo';
import { useVote } from '../hooks/useVote';
import { VoteForm } from '../components/VoteForm';
import { QandAPanel } from '../components/QandAPanel';
import { ShareLink } from '../components/ShareLink';
import { getUserId, isAdmin } from '../auth/session';

export function VotePage() {
  const { code = '' } = useParams<{ code: string }>();
  const navigate = useNavigate();
  const { poll, loading: pollLoading, notFound } = usePollInfo(code);
  const { vote, loading: voteLoading, error, hasVoted } = useVote(code);

  if (pollLoading) {
    return (
      <div className="page">
        <div className="card">
          <p className="muted">Loading…</p>
        </div>
      </div>
    );
  }

  if (notFound || !poll) {
    return (
      <div className="page">
        <div className="card">
          <h1>Poll not found</h1>
          <p className="muted">That poll code doesn’t exist or was removed.</p>
          <Link to="/create" className="btn">Create a poll</Link>
        </div>
      </div>
    );
  }

  const handleVote = async (optionIndex: number, textAnswer?: string) => {
    const result = await vote(optionIndex, textAnswer);
    if (result) navigate(`/poll/${code}/results`);
  };

  const canModerate = isAdmin() || (!!poll.creatorId && poll.creatorId === getUserId());

  return (
    <div className="page">
      <div className="card">
        <h1>{poll.question}</h1>

        {!poll.isActive && (
          <p className="notice notice--closed" role="status">
            <Lock size={16} strokeWidth={2.25} aria-hidden="true" />
            This poll is closed.
          </p>
        )}

        {hasVoted ? (
          <div className="notice notice--voted" role="status">
            <CheckCircle2 size={18} strokeWidth={2.25} aria-hidden="true" />
            <span>
              You’ve already voted on this poll.{' '}
              <Link to={`/poll/${code}/results`}>
                View live results <ArrowRight size={14} strokeWidth={2.25} aria-hidden="true" />
              </Link>
            </span>
          </div>
        ) : (
          <>
            <VoteForm
              type={poll.type}
              options={poll.options}
              onVote={handleVote}
              disabled={voteLoading || !poll.isActive}
              submitting={voteLoading}
            />
            {error && (
              <p className="error" role="alert">
                {error}
              </p>
            )}
          </>
        )}

        <div className="vote-share">
          <p className="muted share-hint">Share this poll to collect votes:</p>
          <ShareLink code={code} />
        </div>
      </div>

      <QandAPanel code={code} canModerate={canModerate} />
    </div>
  );
}
