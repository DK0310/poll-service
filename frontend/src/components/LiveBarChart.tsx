import type { OptionResult } from '../types/poll.types';

interface LiveBarChartProps {
  options: OptionResult[];
  totalVotes: number;
}

export function LiveBarChart({ options, totalVotes }: LiveBarChartProps) {
  const maxCount = Math.max(...options.map((o) => o.voteCount), 1);

  return (
    <div className="bar-chart">
      {options.map((opt) => (
        <div key={opt.optionIndex} className="bar-row">
          <span className="bar-label">{opt.text}</span>
          <div className="bar-track">
            <div
              className="bar-fill"
              style={{
                width: `${(opt.voteCount / maxCount) * 100}%`,
                transition: 'width 0.5s ease-out',
              }}
            />
          </div>
          <span className="bar-count">
            {opt.voteCount} ({opt.percentage}%)
          </span>
        </div>
      ))}
      <p className="total-votes">{totalVotes} total votes</p>
    </div>
  );
}
