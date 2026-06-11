import { Link, useParams } from 'react-router-dom';
import { Lock, BarChart3, ArrowRight, User, Download } from 'lucide-react';
import { useLiveResults } from '../hooks/useLiveResults';
import { usePollInfo } from '../hooks/usePollInfo';
import { LiveBarChart } from '../components/LiveBarChart';
import { QandAPanel } from '../components/QandAPanel';
import { ShareLink } from '../components/ShareLink';
import { getUserId, isAdmin } from '../auth/session';
import { downloadCsv } from '../utils/csv';

// Compact relative time ("just now", "5m", "2h", "3d") for the comment feed.
function relativeTime(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const s = Math.max(0, Math.floor(diff / 1000));
  if (s < 60) return 'just now';
  const m = Math.floor(s / 60);
  if (m < 60) return `${m}m`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h}h`;
  return `${Math.floor(h / 24)}d`;
}

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
          <Link to="/create" className="btn">Create a poll</Link>
        </div>
      </div>
    );
  }

  const isOpenText = results.type === 'OpenText';
  const canModerate = isAdmin() || (!!poll?.creatorId && poll.creatorId === getUserId());

  const exportCsv = () => {
    if (isOpenText) {
      downloadCsv(
        `poll-${code}-results.csv`,
        ['Author', 'Role', 'Answer', 'SubmittedAt'],
        results.textAnswers.map((a) => [a.authorName ?? 'Anonymous', a.authorRole ?? '', a.text, a.votedAt]),
      );
    } else {
      const rows = results.options.map((o) => [o.text, o.voteCount, `${o.percentage}%`]);
      rows.push(['Total', results.totalVotes, '100%']);
      downloadCsv(`poll-${code}-results.csv`, ['Option', 'Votes', 'Percentage'], rows);
    }
  };

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
              <ul className="comment-list">
                {results.textAnswers.map((answer, i) => {
                  const name = answer.authorName ?? 'Anonymous';
                  const initial = (answer.authorName ?? '').trim().charAt(0).toUpperCase();
                  return (
                    <li key={i} className="comment">
                      <span className={`comment__avatar${answer.authorName ? '' : ' comment__avatar--anon'}`} aria-hidden="true">
                        {initial || <User size={16} strokeWidth={2.25} />}
                      </span>
                      <div className="comment__body">
                        <div className="comment__head">
                          <span className="comment__name">{name}</span>
                          {answer.authorRole && <span className="comment__role">{answer.authorRole}</span>}
                          <span className="comment__time">· {relativeTime(answer.votedAt)}</span>
                        </div>
                        <p className="comment__text">{answer.text}</p>
                      </div>
                    </li>
                  );
                })}
              </ul>
            )}
          </div>
        ) : (
          <LiveBarChart options={results.options} totalVotes={results.totalVotes} />
        )}

        <div className="results-foot">
          <div className="results-share">
            <p className="muted share-hint">Share this poll — results update in real time.</p>
            <ShareLink code={code} />
          </div>
          <div className="results-actions">
            <button type="button" className="btn-outline" onClick={exportCsv}>
              <Download size={18} strokeWidth={2.25} aria-hidden="true" /> Download CSV
            </button>
            {canModerate && (
              <Link to={`/poll/${code}/analytics`} className="btn-outline">
                <BarChart3 size={18} strokeWidth={2.25} aria-hidden="true" /> View analytics
                <ArrowRight size={16} strokeWidth={2.25} aria-hidden="true" />
              </Link>
            )}
          </div>
        </div>
      </div>

      <QandAPanel code={code} canModerate={canModerate} />
    </div>
  );
}
