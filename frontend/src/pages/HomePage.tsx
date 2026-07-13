import { useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import {
  ArrowRight,
  Check,
  Crown,
  Zap,
  MessageSquare,
  BarChart3,
  Timer,
  QrCode,
  ShieldCheck,
} from 'lucide-react';

// ── The hero IS a live, votable results board ─────────────────
// Visitors actually answer and watch the bars + count react — the
// page demonstrates the product by being one (show, don't tell).
const DEMO_QUESTION = 'Best way to kick off a meeting?';
const DEMO_OPTIONS = [
  { label: 'Straight to the point', seed: 1240, color: 'tangerine' },
  { label: 'Quick round of hellos', seed: 861, color: 'grape' },
  { label: 'Coffee first', seed: 632, color: 'teal' },
] as const;

const barColor: Record<string, string> = {
  tangerine: 'bg-tangerine',
  grape: 'bg-grape',
  teal: 'bg-teal',
};
const glow: Record<string, string> = {
  tangerine: 'shadow-glow-tangerine',
  grape: 'shadow-glow-grape',
  teal: 'shadow-glow-teal',
};

function HeroBoard() {
  const [choice, setChoice] = useState<number | null>(null);
  const voted = choice !== null;
  const counts = DEMO_OPTIONS.map((o, i) => o.seed + (choice === i ? 1 : 0));
  const total = counts.reduce((a, b) => a + b, 0);
  const max = Math.max(...counts);

  return (
    <div className="rounded-board border border-line bg-panel p-6 shadow-board sm:p-7">
      <div className="flex items-center justify-between gap-3 border-b border-line pb-4">
        <span className="inline-flex items-center gap-2 font-mono text-xs font-medium uppercase tracking-wider text-tangerine">
          <span className="board-pulse inline-block h-2 w-2 rounded-full bg-tangerine shadow-glow-tangerine" />
          {voted ? 'Live results' : 'Live poll'}
        </span>
        <span className="font-mono text-xs tabular-nums text-fg-muted">
          {total.toLocaleString()} votes
        </span>
      </div>

      <h2 className="mt-5 font-display text-2xl font-bold leading-tight text-fg sm:text-[1.7rem]">
        {DEMO_QUESTION}
      </h2>

      <div className="mt-5 flex flex-col gap-2.5">
        {DEMO_OPTIONS.map((o, i) => {
          const pct = Math.round((counts[i] / total) * 100);
          const isPick = choice === i;
          const leading = counts[i] === max;
          return (
            <button
              key={o.label}
              type="button"
              onClick={() => !voted && setChoice(i)}
              disabled={voted}
              aria-label={voted ? `${o.label}: ${pct}%` : `Vote for ${o.label}`}
              className={`relative flex items-center overflow-hidden rounded-xl border px-4 py-3.5 text-left transition duration-200 ease-out ${
                voted
                  ? 'cursor-default border-line bg-panel-2'
                  : 'cursor-pointer border-line bg-panel-2 hover:-translate-y-0.5 hover:border-line-strong'
              }`}
            >
              <span
                aria-hidden="true"
                className={`absolute inset-y-0 left-0 ${barColor[o.color]} ${
                  voted && leading ? glow[o.color] : ''
                } transition-[width] duration-700 ease-out motion-reduce:transition-none`}
                style={{ width: voted ? `${pct}%` : '0%', opacity: leading ? 0.95 : 0.6 }}
              />
              <span className="relative z-10 flex w-full items-center justify-between gap-3">
                <span className="inline-flex items-center gap-2 font-display font-semibold text-fg">
                  {isPick && <Check size={16} strokeWidth={3} className="shrink-0" aria-hidden="true" />}
                  {voted && leading && (
                    <Crown size={15} strokeWidth={2.5} className="shrink-0 text-amber" aria-hidden="true" />
                  )}
                  {o.label}
                </span>
                {voted && <span className="font-mono text-sm font-medium tabular-nums text-fg">{pct}%</span>}
              </span>
            </button>
          );
        })}
      </div>

      <p className="mt-4 font-mono text-xs leading-relaxed text-fg-muted" aria-live="polite">
        {voted
          ? '> vote recorded · this is exactly what your audience sees'
          : '> tap an option — the bars move the moment you do'}
      </p>
    </div>
  );
}

// ── Section 3: each question type as a mini results board ──────
function MiniBoard({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="board-reveal rounded-board border border-line bg-panel p-5">
      <div className="mb-4 flex items-center justify-between border-b border-line pb-3">
        <span className="font-mono text-xs uppercase tracking-wider text-fg-muted">{label}</span>
        <span className="board-pulse inline-block h-1.5 w-1.5 rounded-full bg-teal" />
      </div>
      {children}
    </div>
  );
}

function Bar({ label, pct, color, lead }: { label: string; pct: number; color: string; lead?: boolean }) {
  return (
    <div className="mb-2.5 last:mb-0">
      <div className="mb-1 flex items-baseline justify-between font-display text-sm">
        <span className="font-medium text-fg">{label}</span>
        <span className="font-mono text-xs tabular-nums text-fg-muted">{pct}%</span>
      </div>
      <div className="h-2.5 overflow-hidden rounded-full bg-panel-2">
        <div
          className={`board-bar h-full rounded-full ${barColor[color]} ${lead ? glow[color] : ''}`}
          style={{ width: `${pct}%` }}
        />
      </div>
    </div>
  );
}

const capabilities = [
  { icon: Zap, title: 'Live over WebSockets', body: 'Results stream to every screen the instant a vote lands — no refresh.' },
  { icon: MessageSquare, title: 'Anonymous Q&A', body: 'The room asks and upvotes (one per person); the host pins the best, live.' },
  { icon: BarChart3, title: 'Creator analytics', body: 'Votes over time, the peak minute, and the top option for every poll you own.' },
  { icon: QrCode, title: 'Short link + QR', body: 'A 5-character code or a scannable QR. Vote once per browser, no login.' },
  { icon: Timer, title: 'Auto-close on expiry', body: 'Set it to close in an hour, a day, or a week. Results lock themselves.' },
  { icon: ShieldCheck, title: 'Roles built in', body: 'Guests vote; users own their polls; admins manage everything.' },
] as const;

const rundown = [
  { n: '01', title: 'Create', body: 'Write the question, pick a type, add options and an optional expiry.' },
  { n: '02', title: 'Share', body: 'Drop the short link or show the QR. Anyone in the room can answer.' },
  { n: '03', title: 'Watch live', body: 'Answers and Q&A update in real time. Dig into the analytics after.' },
] as const;

const tickerItems = [
  'LIVE over WebSockets',
  '4 question types',
  'no login to vote',
  'scan-to-join QR',
  'anonymous Q&A',
  'creator analytics',
  'auto-close on expiry',
];

export function HomePage() {
  return (
    <div className="board bg-bg">
      {/* ── Hero ─────────────────────────────────────────── */}
      <section className="board-grid relative overflow-hidden border-b border-line">
        {/* glow wash */}
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0"
          style={{
            background:
              'radial-gradient(45% 60% at 82% 8%, rgba(255,107,61,0.16), transparent 60%),' +
              'radial-gradient(40% 55% at 8% 28%, rgba(139,107,255,0.12), transparent 58%)',
          }}
        />
        <div className="relative mx-auto grid max-w-6xl items-center gap-12 px-6 py-16 md:py-24 lg:grid-cols-[1fr_minmax(380px,460px)]">
          <div className="board-rise">
            <span className="inline-flex items-center gap-2 rounded-full border border-line bg-panel px-3.5 py-1.5 font-mono text-xs uppercase tracking-wider text-fg-muted">
              <span className="board-pulse inline-block h-2 w-2 rounded-full bg-tangerine shadow-glow-tangerine" />
              Real-time polling
            </span>
            <h1 className="mt-5 font-display text-[clamp(2.75rem,6.5vw,5rem)] font-extrabold leading-[0.98] tracking-[-0.03em] text-balance text-fg">
              Ask the room.<br />
              Watch it answer.
            </h1>
            <p className="mt-6 max-w-lg text-lg leading-relaxed text-fg-muted text-pretty">
              Build a poll in seconds, share one link, and the answers land live on the big screen —
              like an election-night board for whatever you’re asking.
            </p>
            <div className="mt-8 flex flex-wrap items-center gap-x-6 gap-y-3">
              <Link
                to="/create"
                className="inline-flex items-center gap-2 rounded-full bg-tangerine px-7 py-3.5 font-display font-semibold text-on-accent shadow-glow-tangerine transition duration-200 ease-out hover:-translate-y-0.5 hover:bg-amber focus-visible:outline-3 focus-visible:outline-offset-2 focus-visible:outline-fg"
              >
                Create a poll — free
                <ArrowRight size={18} strokeWidth={2.5} aria-hidden="true" />
              </Link>
              <a
                href="#how"
                className="font-display font-semibold text-fg underline decoration-tangerine decoration-2 underline-offset-4 transition-colors hover:text-tangerine"
              >
                See how it works
              </a>
            </div>
          </div>

          <div className="board-rise-2 lg:justify-self-end">
            <HeroBoard />
          </div>
        </div>
      </section>

      {/* ── Ticker ───────────────────────────────────────── */}
      <div className="overflow-hidden border-b border-line bg-panel py-3">
        <div className="board-ticker flex w-max items-center gap-0">
          {[0, 1].map((dup) => (
            <div key={dup} className="flex items-center" aria-hidden={dup === 1}>
              {tickerItems.map((t) => (
                <span key={t} className="flex items-center font-mono text-xs uppercase tracking-wider text-fg-muted">
                  <span className="mx-5 h-1 w-1 rounded-full bg-tangerine" />
                  {t}
                </span>
              ))}
            </div>
          ))}
        </div>
      </div>

      {/* ── Every answer, visualized ─────────────────────── */}
      <section id="features" className="mx-auto max-w-6xl px-6 py-20 md:py-24">
        <h2 className="max-w-2xl font-display text-[clamp(1.9rem,3.5vw,2.75rem)] font-extrabold tracking-tight text-balance text-fg">
          Every answer, on the board
        </h2>
        <p className="mt-3 max-w-xl text-lg text-fg-muted">
          Four question types — each one reads live as the votes come in.
        </p>

        <div className="mt-10 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <MiniBoard label="Multiple choice">
            <Bar label="React" pct={58} color="tangerine" lead />
            <Bar label="Vue" pct={27} color="grape" />
            <Bar label="Svelte" pct={15} color="teal" />
          </MiniBoard>

          <MiniBoard label="Rating 1–5">
            <div className="flex items-end gap-2" style={{ height: 96 }} aria-hidden="true">
              {[18, 32, 54, 100, 70].map((h, i) => (
                <div key={i} className="flex flex-1 flex-col items-center gap-1.5">
                  <div className="flex w-full flex-1 items-end">
                    <div
                      className="board-bar w-full rounded-t-md bg-teal"
                      style={{ height: `${h}%`, transformOrigin: 'bottom' }}
                    />
                  </div>
                  <span className="font-mono text-[10px] text-fg-faint">{i + 1}</span>
                </div>
              ))}
            </div>
            <p className="mt-3 font-mono text-xs text-fg-muted">avg 4.2 · 318 ratings</p>
          </MiniBoard>

          <MiniBoard label="Yes / No">
            <Bar label="Yes" pct={71} color="tangerine" lead />
            <Bar label="No" pct={29} color="grape" />
            <p className="mt-3 font-mono text-xs text-fg-muted">204 votes · live</p>
          </MiniBoard>

          <MiniBoard label="Open text">
            <ul className="flex flex-col gap-2.5">
              {[
                { who: 'AB', name: 'Ada', text: 'Ship it 🚀' },
                { who: '·', name: 'Anonymous', text: 'Needs more tests' },
              ].map((c) => (
                <li key={c.text} className="flex items-start gap-2.5">
                  <span className="grid h-7 w-7 shrink-0 place-items-center rounded-full bg-panel-2 font-mono text-[11px] text-fg-muted">
                    {c.who}
                  </span>
                  <div className="min-w-0">
                    <p className="font-display text-xs font-semibold text-fg">{c.name}</p>
                    <p className="truncate text-sm text-fg-muted">{c.text}</p>
                  </div>
                </li>
              ))}
            </ul>
          </MiniBoard>
        </div>
      </section>

      {/* ── Capabilities ─────────────────────────────────── */}
      <section className="border-y border-line bg-panel">
        <div className="mx-auto max-w-6xl px-6 py-20 md:py-24">
          <h2 className="max-w-2xl font-display text-[clamp(1.9rem,3.5vw,2.75rem)] font-extrabold tracking-tight text-balance text-fg">
            A control room for the live moment
          </h2>
          <div className="mt-10 grid grid-cols-1 gap-x-12 gap-y-8 sm:grid-cols-2">
            {capabilities.map(({ icon: Icon, title, body }) => (
              <div key={title} className="board-reveal flex gap-4 border-t border-line pt-6">
                <Icon size={22} strokeWidth={2.25} className="mt-0.5 shrink-0 text-tangerine" aria-hidden="true" />
                <div>
                  <h3 className="font-display text-lg font-bold text-fg">{title}</h3>
                  <p className="mt-1 text-[0.95rem] leading-relaxed text-fg-muted">{body}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ── The rundown ──────────────────────────────────── */}
      <section id="how" className="mx-auto max-w-6xl px-6 py-20 md:py-24">
        <h2 className="font-display text-[clamp(1.9rem,3.5vw,2.75rem)] font-extrabold tracking-tight text-balance text-fg">
          Idea to live results in three
        </h2>
        <ol className="mt-12 grid grid-cols-1 gap-10 md:grid-cols-3">
          {rundown.map(({ n, title, body }) => (
            <li key={n} className="board-reveal border-t-2 border-line-strong pt-5">
              <span className="font-mono text-5xl font-semibold tabular-nums text-tangerine">{n}</span>
              <h3 className="mt-3 font-display text-xl font-bold text-fg">{title}</h3>
              <p className="mt-1.5 text-[0.95rem] leading-relaxed text-fg-muted">{body}</p>
            </li>
          ))}
        </ol>
      </section>

      {/* ── CTA band ─────────────────────────────────────── */}
      <section className="board-grid relative overflow-hidden border-t border-line">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0"
          style={{ background: 'radial-gradient(50% 80% at 50% 0%, rgba(255,107,61,0.18), transparent 65%)' }}
        />
        <div className="relative mx-auto max-w-3xl px-6 py-20 text-center md:py-28">
          <div className="board-reveal">
            <h2 className="font-display text-[clamp(2.25rem,5vw,3.5rem)] font-extrabold leading-[1.02] tracking-[-0.02em] text-balance text-fg">
              Put your question on the board.
            </h2>
            <p className="mx-auto mt-5 max-w-md text-lg text-fg-muted">
              Free to use. Create an account and share a link in under a minute.
            </p>
            <Link
              to="/create"
              className="mt-9 inline-flex items-center gap-2 rounded-full bg-tangerine px-8 py-4 font-display font-semibold text-on-accent shadow-glow-tangerine transition duration-200 ease-out hover:-translate-y-0.5 hover:bg-amber focus-visible:outline-3 focus-visible:outline-offset-2 focus-visible:outline-fg"
            >
              Create a poll — free
              <ArrowRight size={18} strokeWidth={2.5} aria-hidden="true" />
            </Link>
          </div>
        </div>
      </section>
    </div>
  );
}
