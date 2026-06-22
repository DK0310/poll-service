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
      <div className="board mx-auto w-full max-w-2xl">
        <div className="board-panel p-7 sm:p-9">
          <p className="text-fg-muted">Loading results…</p>
        </div>
      </div>
    );
  }

  if (notFound || !results) {
    return (
      <div className="board mx-auto w-full max-w-2xl">
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
    <div className="board mx-auto w-full max-w-2xl">
      <div className="board-panel board-grid p-7 sm:p-9">
        <div className="mb-6 flex flex-wrap items-center justify-between gap-2">
          <h1 className="font-display text-2xl font-bold leading-tight tracking-tight text-fg text-balance sm:text-3xl">
            {results.question}
          </h1>
          {connected ? (
            <span className="inline-flex flex-none items-center gap-2 font-mono text-sm text-amber" role="status">
              <span className="board-pulse h-2.5 w-2.5 rounded-full bg-amber shadow-[0_0_10px_var(--color-amber)]" />
              LIVE
            </span>
          ) : (
            <span className="inline-flex flex-none items-center gap-2 font-mono text-sm text-fg-muted" role="status">
              <span className="h-2.5 w-2.5 rounded-full bg-fg-muted" />
              Connecting…
            </span>
          )}
        </div>

        {!results.isActive && (
          <p
            className="mb-4 flex items-center gap-2 rounded-lg border border-line bg-panel-2 px-4 py-3 text-sm text-fg-muted"
            role="status"
          >
            <Lock size={16} strokeWidth={2.25} aria-hidden="true" /> Poll closed — final results.
          </p>
        )}

        {isOpenText ? (
          <div>
            <div className="mb-6 flex items-baseline gap-2">
              <span className="font-display text-4xl font-bold tabular-nums text-tangerine">
                {results.totalVotes}
              </span>
              <span className="text-sm text-fg-muted">
                {results.totalVotes === 1 ? 'response' : 'responses'}
              </span>
            </div>
            {results.textAnswers.length === 0 ? (
              <p className="text-fg-muted">No responses yet — share the link to collect answers.</p>
            ) : (
              <ul className="flex flex-col gap-4">
                {results.textAnswers.map((answer, i) => {
                  const name = answer.authorName ?? 'Anonymous';
                  const initial = (answer.authorName ?? '').trim().charAt(0).toUpperCase();
                  return (
                    <li key={i} className="flex items-start gap-2.5">
                      <span
                        className={[
                          'grid h-9 w-9 flex-none place-items-center rounded-full text-[15px] font-bold text-bg',
                          answer.authorName ? 'bg-tangerine' : 'bg-fg-muted',
                        ].join(' ')}
                        aria-hidden="true"
                      >
                        {initial || <User size={16} strokeWidth={2.25} />}
                      </span>
                      <div className="min-w-0 flex-1 rounded-lg border border-line bg-panel-2 px-3.5 py-2.5">
                        <div className="mb-1 flex flex-wrap items-baseline gap-1.5">
                          <span className="font-semibold text-fg">{name}</span>
                          {answer.authorRole && (
                            <span className="rounded-full bg-tangerine/15 px-1.5 py-px text-[11px] font-semibold uppercase tracking-wide text-tangerine">
                              {answer.authorRole}
                            </span>
                          )}
                          <span className="text-xs text-fg-muted">· {relativeTime(answer.votedAt)}</span>
                        </div>
                        <p className="whitespace-pre-wrap break-words text-fg-muted">{answer.text}</p>
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

        <div className="mt-8 flex flex-wrap items-center justify-between gap-4 border-t border-line pt-6">
          <div className="min-w-0 flex-1 basis-80">
            <p className="mb-2 text-sm text-fg-muted">Share this poll — results update in real time.</p>
            <ShareLink code={code} />
          </div>
          <div className="flex flex-wrap gap-2">
            <button type="button" className="board-btn-outline" onClick={exportCsv}>
              <Download size={18} strokeWidth={2.25} aria-hidden="true" /> Download CSV
            </button>
            {canModerate && (
              <Link to={`/poll/${code}/analytics`} className="board-btn-outline">
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
