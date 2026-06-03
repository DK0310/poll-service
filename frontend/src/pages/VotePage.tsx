import { Link, useNavigate, useParams } from 'react-router-dom';
import { usePollInfo } from '../hooks/usePollInfo';
import { useVote } from '../hooks/useVote';
import { VoteForm } from '../components/VoteForm';
import { QandAPanel } from '../components/QandAPanel';

export function VotePage() {
  const { code = '' } = useParams<{ code: string }>();
  const navigate = useNavigate();
  const { poll, loading: pollLoading, notFound } = usePollInfo(code);
  const { vote, loading: voteLoading, error, hasVoted } = useVote(code);

  if (pollLoading) return <p className="page">Loading…</p>;
  if (notFound || !poll) return <p className="page">Poll not found.</p>;

  const handleVote = async (optionIndex: number, textAnswer?: string) => {
    const result = await vote(optionIndex, textAnswer);
    if (result) navigate(`/poll/${code}/results`);
  };

  return (
    <div className="page">
      <h1>{poll.question}</h1>
      {!poll.isActive && <p className="closed-banner">This poll is closed.</p>}
      {hasVoted ? (
        <p>
          You have already voted. <Link to={`/poll/${code}/results`}>View results →</Link>
        </p>
      ) : (
        <>
          <VoteForm
            type={poll.type}
            options={poll.options}
            onVote={handleVote}
            disabled={voteLoading || !poll.isActive}
          />
          {error && <p className="error">{error}</p>}
        </>
      )}
      <QandAPanel code={code} />
    </div>
  );
}
