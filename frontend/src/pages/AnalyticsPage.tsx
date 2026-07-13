import { Link, useLocation, useParams } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { useAnalytics } from '../hooks/useAnalytics';
import { LineChart } from '../components/LineChart';

const hhmm = (iso: string) =>
  new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

export function AnalyticsPage() {
  const { code = '' } = useParams<{ code: string }>();
  const { state } = useLocation();
  const fromMyPolls = (state as { from?: string } | null)?.from === 'my-polls';
  const backTo = fromMyPolls ? '/my-polls' : `/poll/${code}/results`;
  const backLabel = fromMyPolls ? 'Back to my polls' : 'Back to live results';
  const { analytics, loading, notFound } = useAnalytics(code);

  if (loading) {
    return (
      <div className="page">
        <div className="card">
          <div className="skeleton skeleton--title" />
          <div className="stat-grid">
            <div className="skeleton skeleton--stat" />
            <div className="skeleton skeleton--stat" />
            <div className="skeleton skeleton--stat" />
          </div>
        </div>
        <div className="card">
          <div className="skeleton skeleton--chart" />
        </div>
      </div>
    );
  }

  if (notFound || !analytics) {
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

  const points = analytics.timeline.map((b) => ({ label: hhmm(b.minute), value: b.count }));

  return (
    <div className="page">
      <div className="card">
        <div className="analytics-head">
          <h1>Analytics</h1>
          <p className="muted">{analytics.title ?? 'Untitled survey'}</p>
        </div>

        <div className="stat-grid">
          <div className="stat-card">
            <span className="stat-card__num h-gradient tnum">{analytics.totalVoters}</span>
            <span className="stat-card__label">Total voters</span>
          </div>
          <div className="stat-card">
            <span className="stat-card__num h-gradient tnum">{analytics.questions.length}</span>
            <span className="stat-card__label">Questions</span>
          </div>
          <div className="stat-card">
            <span className="stat-card__num h-gradient tnum">
              {analytics.peakMinute ? hhmm(analytics.peakMinute.minute) : '—'}
            </span>
            <span className="stat-card__label">
              Peak minute{analytics.peakMinute ? ` · ${analytics.peakMinute.count} voters` : ''}
            </span>
          </div>
        </div>
      </div>

      <div className="card chart-card">
        <h2 className="chart-card__title">Submissions over time</h2>
        <LineChart points={points} />
      </div>

      <div className="card">
        <h2 className="chart-card__title">Per-question breakdown</h2>
        <ul className="mt-4 flex flex-col gap-3">
          {analytics.questions.map((q, i) => (
            <li
              key={q.questionId}
              className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-line bg-panel-2 px-4 py-3"
            >
              <span className="min-w-0 flex-1 basis-64 font-display font-semibold text-fg">
                <span className="mr-2 font-mono text-sm text-tangerine tabular-nums">
                  {String(i + 1).padStart(2, '0')}
                </span>
                {q.text}
              </span>
              <span className="font-mono text-sm text-fg-muted">
                {q.type === 'OpenText'
                  ? `${q.totalVotes} ${q.totalVotes === 1 ? 'response' : 'responses'}`
                  : q.topOption
                    ? `Top: ${q.topOption.text} · ${q.topOption.voteCount}/${q.totalVotes}`
                    : 'No votes yet'}
              </span>
            </li>
          ))}
        </ul>
      </div>

      <Link to={backTo} className="btn-outline analytics-back">
        <ArrowLeft size={18} strokeWidth={2.25} aria-hidden="true" /> {backLabel}
      </Link>
    </div>
  );
}
