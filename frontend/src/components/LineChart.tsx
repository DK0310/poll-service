import { useId, useState } from 'react';
import { Table2 } from 'lucide-react';

interface Point {
  label: string;
  value: number;
}

interface LineChartProps {
  points: Point[];
}

// Dependency-free SVG line chart for the votes-over-time series.
export function LineChart({ points }: LineChartProps) {
  const [showTable, setShowTable] = useState(false);
  const gradId = `area-${useId().replace(/:/g, '')}`;

  if (points.length === 0) {
    return (
      <p className="muted results-empty">
        No votes yet — the timeline fills in as votes arrive.
      </p>
    );
  }

  const width = 560;
  const height = 200;
  const padX = 30;
  const padY = 24;
  const max = Math.max(...points.map((p) => p.value), 1);
  const stepX = points.length > 1 ? (width - padX * 2) / (points.length - 1) : 0;

  const xy = (i: number, value: number) => ({
    x: padX + i * stepX,
    y: height - padY - (value / max) * (height - padY * 2),
  });

  const linePts = points.map((p, i) => {
    const { x, y } = xy(i, p.value);
    return `${x},${y}`;
  });
  const lastX = padX + (points.length - 1) * stepX;
  const areaPts = `${padX},${height - padY} ${linePts.join(' ')} ${lastX},${height - padY}`;

  const peak = points.reduce((a, b) => (b.value > a.value ? b : a), points[0]);
  const total = points.reduce((s, p) => s + p.value, 0);
  const summary = `Votes over time: ${total} total across ${points.length} intervals, peaking at ${peak.value} around ${peak.label}.`;

  const gridFractions = [0, 0.25, 0.5, 0.75, 1];
  const labelIdx =
    points.length <= 1 ? [0] : [0, Math.floor((points.length - 1) / 2), points.length - 1];

  return (
    <div className="line-chart-wrap">
      <svg
        viewBox={`0 0 ${width} ${height}`}
        className="line-chart"
        role="img"
        aria-label={summary}
        preserveAspectRatio="none"
      >
        <defs>
          <linearGradient id={gradId} x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="var(--blue)" stopOpacity="0.2" />
            <stop offset="100%" stopColor="var(--blue)" stopOpacity="0" />
          </linearGradient>
        </defs>

        {/* hairline gridlines */}
        {gridFractions.map((f) => {
          const y = height - padY - f * (height - padY * 2);
          return (
            <line
              key={f}
              x1={padX}
              y1={y}
              x2={width - padX}
              y2={y}
              stroke="rgba(46,22,32,0.08)"
              strokeWidth="1"
            />
          );
        })}

        {/* area + line */}
        <polygon points={areaPts} fill={`url(#${gradId})`} />
        <polyline
          points={linePts.join(' ')}
          fill="none"
          stroke="var(--blue)"
          strokeWidth="2.5"
          strokeLinejoin="round"
          strokeLinecap="round"
        />

        {/* points */}
        {points.map((p, i) => {
          const { x, y } = xy(i, p.value);
          return <circle key={i} cx={x} cy={y} r="3.5" fill="var(--blue)" />;
        })}

        {/* y max + x labels (mono via CSS) */}
        <text x={padX} y={padY - 8} className="chart-axis" textAnchor="start">
          {max}
        </text>
        {labelIdx.map((i) => {
          const { x } = xy(i, points[i].value);
          const anchor = i === 0 ? 'start' : i === points.length - 1 ? 'end' : 'middle';
          return (
            <text key={i} x={x} y={height - 6} className="chart-axis" textAnchor={anchor}>
              {points[i].label}
            </text>
          );
        })}
      </svg>

      <button
        type="button"
        className="btn-link chart-table-toggle"
        onClick={() => setShowTable((s) => !s)}
        aria-expanded={showTable}
      >
        <Table2 size={15} strokeWidth={2.25} aria-hidden="true" />
        {showTable ? 'Hide data table' : 'Show data table'}
      </button>

      {showTable && (
        <table className="data-table">
          <caption className="sr-only">{summary}</caption>
          <thead>
            <tr>
              <th scope="col">Time</th>
              <th scope="col">Votes</th>
            </tr>
          </thead>
          <tbody>
            {points.map((p, i) => (
              <tr key={i}>
                <th scope="row" className="mono">{p.label}</th>
                <td className="mono tnum">{p.value}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}
