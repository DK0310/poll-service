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
          <p className="muted">{analytics.question}</p>
        </div>

        <div className="stat-grid">
          <div className="stat-card">
            <span className="stat-card__num h-gradient tnum">{analytics.totalVotes}</span>
            <span className="stat-card__label">Total votes</span>
          </div>
          <div className="stat-card">
            <span className="stat-card__num h-gradient">{analytics.topOption?.text ?? '—'}</span>
            <span className="stat-card__label">
              Top option{analytics.topOption ? ` · ${analytics.topOption.voteCount} votes` : ''}
            </span>
          </div>
          <div className="stat-card">
            <span className="stat-card__num h-gradient tnum">
              {analytics.peakMinute ? hhmm(analytics.peakMinute.minute) : '—'}
            </span>
            <span className="stat-card__label">
              Peak minute{analytics.peakMinute ? ` · ${analytics.peakMinute.count} votes` : ''}
            </span>
          </div>
        </div>
      </div>

      <div className="card chart-card">
        <h2 className="chart-card__title">Votes over time</h2>
        <LineChart points={points} />
      </div>

      <Link to={backTo} className="btn-outline analytics-back">
        <ArrowLeft size={18} strokeWidth={2.25} aria-hidden="true" /> {backLabel}
      </Link>
    </div>
  );
}
