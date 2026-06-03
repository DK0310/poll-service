import { Link, useParams } from 'react-router-dom';
import { useAnalytics } from '../hooks/useAnalytics';
import { LineChart } from '../components/LineChart';

const hhmm = (iso: string) =>
  new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

export function AnalyticsPage() {
  const { code = '' } = useParams<{ code: string }>();
  const { analytics, loading, notFound } = useAnalytics(code);

  if (loading) return <p className="page">Loading analytics…</p>;
  if (notFound || !analytics) return <p className="page">Poll not found.</p>;

  const points = analytics.timeline.map((b) => ({ label: hhmm(b.minute), value: b.count }));

  return (
    <div className="page">
      <h1>Analytics</h1>
      <p className="created-question">{analytics.question}</p>

      <div className="stat-grid">
        <div className="stat">
          <span className="stat-num">{analytics.totalVotes}</span>
          <span className="stat-label">total votes</span>
        </div>
        <div className="stat">
          <span className="stat-num">{analytics.topOption?.text ?? '—'}</span>
          <span className="stat-label">
            top option{analytics.topOption ? ` (${analytics.topOption.voteCount})` : ''}
          </span>
        </div>
        <div className="stat">
          <span className="stat-num">{analytics.peakMinute ? hhmm(analytics.peakMinute.minute) : '—'}</span>
          <span className="stat-label">
            peak minute{analytics.peakMinute ? ` (${analytics.peakMinute.count})` : ''}
          </span>
        </div>
      </div>

      <h2>Votes over time</h2>
      <LineChart points={points} />

      <p className="share-hint">
        <Link to={`/poll/${code}/results`}>← Back to live results</Link>
      </p>
    </div>
  );
}
