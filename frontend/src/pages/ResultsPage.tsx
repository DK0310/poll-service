import { Link, useParams } from 'react-router-dom';
import { Lock, BarChart3, ArrowRight } from 'lucide-react';
import { useLiveResults } from '../hooks/useLiveResults';
import { usePollInfo } from '../hooks/usePollInfo';
import { LiveBarChart } from '../components/LiveBarChart';
import { QandAPanel } from '../components/QandAPanel';
import { getUserId, isAdmin } from '../auth/session';

export function ResultsPage() {
  const { code = '' } = useParams<{ code: string }>();
  const { results, loading, notFound, connected } = useLiveResults(code);
  const { poll } = usePollInfo(code); // for ownership-gated analytics link

  if (loading) {
    return (
      <div className="page">
        <div className="card">
          <p className="muted">Loading results…</p>
        </div>
      </div>
    );
  }

  if (notFound || !results) {
    return (
      <div className="page">
        <div className="card">
          <h1>Poll not found</h1>
          <p className="muted">That poll code doesn’t exist or was removed.</p>
          <Link to="/" className="btn">Create a poll</Link>
        </div>
      </div>
    );
  }

  const isOpenText = results.type === 'OpenText';
  const canModerate = isAdmin() || (!!poll?.creatorId && poll.creatorId === getUserId());

  return (
    <div className="page">
      <div className="card">
        <div className="results-head">
          <h1>{results.question}</h1>
          <span className={`live-badge${connected ? '' : ' live-badge--off'}`} role="status">
            {connected ? 'Live' : 'Connecting…'}
          </span>
        </div>

        {!results.isActive && (
          <p className="notice notice--closed" role="status">
            <Lock size={16} strokeWidth={2.25} aria-hidden="true" /> Poll closed — final results.
          </p>
        )}

        {isOpenText ? (
          <div className="text-answers">
            <div className="kpi">
              <span className="kpi__num h-gradient tnum">{results.totalVotes}</span>
              <span className="kpi__label">{results.totalVotes === 1 ? 'response' : 'responses'}</span>
            </div>
            {results.textAnswers.length === 0 ? (
              <p className="muted results-empty">No responses yet — share the link to collect answers.</p>
            ) : (
              <ul className="answer-list">
                {results.textAnswers.map((answer, i) => (
                  <li key={i} className="answer-item">{answer}</li>
                ))}
              </ul>
            )}
          </div>
        ) : (
          <LiveBarChart options={results.options} totalVotes={results.totalVotes} />
        )}

        <div className="results-foot">
          <p className="muted share-hint">Share this page — results update in real time.</p>
          {canModerate && (
            <Link to={`/poll/${code}/analytics`} className="btn-outline">
              <BarChart3 size={18} strokeWidth={2.25} aria-hidden="true" /> View analytics
              <ArrowRight size={16} strokeWidth={2.25} aria-hidden="true" />
            </Link>
          )}
        </div>
      </div>

      <QandAPanel code={code} canModerate={canModerate} />
    </div>
  );
}
