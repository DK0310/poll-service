interface Point {
  label: string;
  value: number;
}

interface LineChartProps {
  points: Point[];
}

// Dependency-free SVG line chart for the votes-over-time series.
export function LineChart({ points }: LineChartProps) {
  if (points.length === 0) return <p className="muted">No votes yet.</p>;

  const width = 480;
  const height = 160;
  const pad = 24;
  const max = Math.max(...points.map((p) => p.value), 1);
  const stepX = points.length > 1 ? (width - pad * 2) / (points.length - 1) : 0;

  const xy = (i: number, value: number) => {
    const x = pad + i * stepX;
    const y = height - pad - (value / max) * (height - pad * 2);
    return { x, y };
  };

  const polyline = points.map((p, i) => {
    const { x, y } = xy(i, p.value);
    return `${x},${y}`;
  }).join(' ');

  return (
    <svg viewBox={`0 0 ${width} ${height}`} className="line-chart" role="img" aria-label="Votes over time">
      <polyline points={polyline} fill="none" stroke="var(--accent)" strokeWidth="2" />
      {points.map((p, i) => {
        const { x, y } = xy(i, p.value);
        return <circle key={i} cx={x} cy={y} r="3" fill="var(--accent)" />;
      })}
    </svg>
  );
}
