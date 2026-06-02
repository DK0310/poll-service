import { useParams } from 'react-router-dom';
import { useLiveResults } from '../hooks/useLiveResults';
import { LiveBarChart } from '../components/LiveBarChart';

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
      <LiveBarChart options={results.options} totalVotes={results.totalVotes} />
      <p className="share-hint">Share this page — results update in real time!</p>
    </div>
  );
}
