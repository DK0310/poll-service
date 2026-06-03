import { Crown } from 'lucide-react';
import type { OptionResult } from '../types/poll.types';

interface LiveBarChartProps {
  options: OptionResult[];
  totalVotes: number;
}

const NON_LEAD_FILL = ['bar-fill--blue', 'bar-fill--violet'];

export function LiveBarChart({ options, totalVotes }: LiveBarChartProps) {
  const maxCount = Math.max(...options.map((o) => o.voteCount), 0);

  return (
    <div className="bar-chart">
      <div className="kpi">
        <span className="kpi__num h-gradient tnum">{totalVotes}</span>
        <span className="kpi__label">total {totalVotes === 1 ? 'vote' : 'votes'}</span>
      </div>

      {totalVotes === 0 && (
        <p className="muted results-empty">No votes yet — share the link to get the first one in.</p>
      )}

      <div className="bar-list">
        {options.map((opt, i) => {
          const isLeader = maxCount > 0 && opt.voteCount === maxCount;
          const fillClass = isLeader ? 'bar-fill--lead' : NON_LEAD_FILL[i % NON_LEAD_FILL.length];
          return (
            <div key={opt.optionIndex} className="bar-row">
              <div className="bar-row__head">
                <span className="bar-row__label">
                  {opt.text}
                  {isLeader && totalVotes > 0 && (
                    <span className="lead-tag">
                      <Crown size={12} strokeWidth={2.5} aria-hidden="true" /> Leading
                    </span>
                  )}
                </span>
                <span className="bar-row__fig mono tnum">
                  {opt.voteCount} · {opt.percentage}%
                </span>
              </div>
              <div className="bar-track">
                <div
                  className={`bar-fill ${fillClass}`}
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
