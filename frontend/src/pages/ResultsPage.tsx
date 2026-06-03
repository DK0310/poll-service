import { Link, useParams } from 'react-router-dom';
import { useLiveResults } from '../hooks/useLiveResults';
import { LiveBarChart } from '../components/LiveBarChart';
import { QandAPanel } from '../components/QandAPanel';

export function ResultsPage() {
  const { code = '' } = useParams<{ code: string }>();
  const { results, loading, notFound, connected } = useLiveResults(code);

  if (loading) return <p className="page">Loading results…</p>;
  if (notFound || !results) return <p className="page">Poll not found.</p>;

  return (
    <div className="page">
      <h1>{results.question}</h1>
      <span className="live-badge">{connected ? '● Live' : '○ Connecting…'}</span>
      {!results.isActive && <p className="closed-banner">Poll closed — final results.</p>}
      {results.type === 'OpenText' ? (
        <div className="text-answers">
          <p className="total-votes">{results.totalVotes} response(s)</p>
          {results.textAnswers.length === 0 ? (
            <p className="muted">No responses yet.</p>
          ) : (
            <ul>
              {results.textAnswers.map((answer, i) => (
                <li key={i} className="text-answer-item">{answer}</li>
              ))}
            </ul>
          )}
        </div>
      ) : (
        <LiveBarChart options={results.options} totalVotes={results.totalVotes} />
      )}
      <p className="share-hint">
        Share this page — results update in real time! ·{' '}
        <Link to={`/poll/${code}/analytics`}>View analytics →</Link>
      </p>
      <QandAPanel code={code} />
    </div>
  );
}
