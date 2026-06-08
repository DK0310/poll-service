import { Link } from 'react-router-dom';
import {
  Sparkles,
  Crown,
  LayoutGrid,
  Zap,
  Link2,
  MessageSquare,
  BarChart3,
  Timer,
} from 'lucide-react';

const features = [
  {
    icon: LayoutGrid,
    title: 'Four question types',
    body: 'Multiple choice, Yes / No, Rating 1–5, and Open text — with 2–6 options where it counts.',
  },
  {
    icon: Zap,
    title: 'Live results',
    body: 'Bar charts update the instant anyone votes, streamed over WebSockets (SignalR).',
  },
  {
    icon: Link2,
    title: 'Share a short link',
    body: 'Every poll gets a 5-character code. Vote once per browser — no login required.',
  },
  {
    icon: MessageSquare,
    title: 'Anonymous Q&A',
    body: 'The audience asks questions and upvotes (one per person); the host can pin the best — all live.',
  },
  {
    icon: BarChart3,
    title: 'Creator analytics',
    body: 'Votes over time, peak voting minute, and the top option for every poll you own.',
  },
  {
    icon: Timer,
    title: 'Auto-close on expiry',
    body: 'Set a poll to close in an hour, a day, or a week — final results lock automatically.',
  },
];

export function HomePage() {
  return (
    <>
      {/* ── Hero ─────────────────────────────────────────── */}
      <section className="hero">
        <div className="lp-wrap">
          <div className="hero-grid">
            <div>
              <span className="chip chip--pink">
                <Sparkles size={14} strokeWidth={2.25} aria-hidden="true" /> Real-time polls &amp; surveys
              </span>
              <h1>
                Create a poll.
                <br />
                Watch votes update <span className="h-gradient">live.</span>
              </h1>
              <p className="lead">
                Build a poll in seconds, share a short link, and see results stream in on a live bar
                chart — no page refresh, no account needed to vote.
              </p>
              <div className="hero-cta">
                <Link to="/create" className="btn">
                  Create a poll — free
                </Link>
                <a href="#how" className="btn-outline">
                  See how it works
                </a>
              </div>
              <p className="hero-note">Free to use · sign in only to create &amp; manage polls.</p>
            </div>

            {/* faux live-results preview */}
            <div className="preview-card" aria-hidden="true">
              <div className="preview-top">
                <strong style={{ fontSize: 15 }}>How did this sprint feel?</strong>
                <span className="live-badge">Live</span>
              </div>
              <div className="kpi" style={{ marginBottom: 16 }}>
                <span className="kpi__num h-gradient tnum">124</span>
                <span className="kpi__label">votes</span>
              </div>
              <div className="bar-list">
                <div className="bar-row">
                  <div className="bar-row__head">
                    <span className="bar-row__label">
                      Good <span className="lead-tag"><Crown size={12} strokeWidth={2.5} /> Leading</span>
                    </span>
                    <span className="bar-row__fig">55</span>
                  </div>
                  <div className="bar-track">
                    <div className="bar-fill bar-fill--lead" style={{ transform: 'scaleX(1)' }} />
                  </div>
                </div>
                <div className="bar-row">
                  <div className="bar-row__head">
                    <span className="bar-row__label">Great</span>
                    <span className="bar-row__fig">42</span>
                  </div>
                  <div className="bar-track">
                    <div className="bar-fill bar-fill--blue" style={{ transform: 'scaleX(0.76)' }} />
                  </div>
                </div>
                <div className="bar-row">
                  <div className="bar-row__head">
                    <span className="bar-row__label">Meh</span>
                    <span className="bar-row__fig">18</span>
                  </div>
                  <div className="bar-track">
                    <div className="bar-fill bar-fill--violet" style={{ transform: 'scaleX(0.33)' }} />
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ── Features ─────────────────────────────────────── */}
      <section className="lp-section lp-section--alt" id="features">
        <div className="lp-wrap">
          <h2>Everything you need to run a live poll</h2>
          <p className="sub">Pick a question type, share a link, and engage your audience in real time.</p>
          <div className="feat-grid">
            {features.map(({ icon: Icon, title, body }) => (
              <div className="feat" key={title}>
                <div className="feat__ic">
                  <Icon size={22} strokeWidth={2.25} aria-hidden="true" />
                </div>
                <h3>{title}</h3>
                <p>{body}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* ── How it works ─────────────────────────────────── */}
      <section className="lp-section" id="how">
        <div className="lp-wrap">
          <h2>How it works</h2>
          <p className="sub">From idea to live results in three steps.</p>
          <div className="steps">
            <div className="step">
              <div className="step__n">1</div>
              <h3>Create</h3>
              <p>Sign in, write your question, pick a type, add options and an optional expiry.</p>
            </div>
            <div className="step">
              <div className="step__n">2</div>
              <h3>Share</h3>
              <p>Send the short <span className="mono">/poll/code</span> link. Anyone can vote — no account required.</p>
            </div>
            <div className="step">
              <div className="step__n">3</div>
              <h3>Watch live</h3>
              <p>Results and Q&amp;A update in real time. Dig into analytics when you're done.</p>
            </div>
          </div>
        </div>
      </section>

      {/* ── CTA band ─────────────────────────────────────── */}
      <section className="cta-band">
        <div className="lp-wrap">
          <h2>Ready to run your first poll?</h2>
          <p>Free to use. Create an account and share a link in under a minute.</p>
          <Link to="/create" className="btn">
            Create a poll — free
          </Link>
        </div>
      </section>
    </>
  );
}
