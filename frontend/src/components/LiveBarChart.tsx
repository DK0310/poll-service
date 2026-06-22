import { Crown } from 'lucide-react';
import type { OptionResult } from '../types/poll.types';

interface LiveBarChartProps {
  options: OptionResult[];
  totalVotes: number;
}

const NON_LEAD_FILL = ['board-bar-fill--teal', 'board-bar-fill--grape'];

export function LiveBarChart({ options, totalVotes }: LiveBarChartProps) {
  const maxCount = Math.max(...options.map((o) => o.voteCount), 0);

  return (
    <div>
      <div className="mb-6 flex items-baseline gap-2">
        <span className="font-display text-4xl font-bold tabular-nums text-tangerine">{totalVotes}</span>
        <span className="text-sm text-fg-muted">total {totalVotes === 1 ? 'vote' : 'votes'}</span>
      </div>

      {totalVotes === 0 && (
        <p className="mb-6 text-fg-muted">No votes yet — share the link to get the first one in.</p>
      )}

      <div className="flex flex-col gap-4">
        {options.map((opt, i) => {
          const isLeader = maxCount > 0 && opt.voteCount === maxCount;
          const fillClass = isLeader ? 'board-bar-fill--lead' : NON_LEAD_FILL[i % NON_LEAD_FILL.length];
          return (
            <div key={opt.optionIndex}>
              <div className="mb-1.5 flex items-baseline justify-between gap-2">
                <span className="inline-flex items-center gap-2 font-display font-semibold text-fg">
                  {opt.text}
                  {isLeader && totalVotes > 0 && (
                    <span className="inline-flex items-center gap-1 rounded-full bg-tangerine/20 px-2 py-0.5 font-display text-[11px] font-semibold text-tangerine">
                      <Crown size={12} strokeWidth={2.5} aria-hidden="true" /> Leading
                    </span>
                  )}
                </span>
                <span className="font-mono text-sm tabular-nums text-fg-muted">
                  {opt.voteCount} · {opt.percentage}%
                </span>
              </div>
              <div className="board-bar-track">
                <div
                  className={`board-bar-fill ${fillClass}`}
                  style={{ transform: `scaleX(${opt.percentage / 100})` }}
                />
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
