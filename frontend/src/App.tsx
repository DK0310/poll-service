import { useEffect } from 'react';
import { BrowserRouter, NavLink, Link, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import { Vote, LogOut, ShieldCheck } from 'lucide-react';
import { HomePage } from './pages/HomePage';
import { CreatePollPage } from './pages/CreatePollPage';
import { VotePage } from './pages/VotePage';
import { ResultsPage } from './pages/ResultsPage';
import { AnalyticsPage } from './pages/AnalyticsPage';
import { MyPollsPage } from './pages/MyPollsPage';
import { AdminDashboardPage } from './pages/AdminDashboardPage';
import { LoginPage } from './pages/LoginPage';
import { RegisterPage } from './pages/RegisterPage';
import { RequireAuth } from './components/RequireAuth';
import { RequireAdmin } from './components/RequireAdmin';
import { useAuthStatus } from './hooks/useAuthStatus';
import { warmBackend } from './api/warmup';
import { clearToken } from './auth/session';
import { ToastProvider } from './components/Toast';

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  isActive ? 'nav-link nav-link--active' : 'nav-link';

function Nav() {
  const navigate = useNavigate();
  const { authed, isAdmin: admin } = useAuthStatus();

  const logout = () => {
    clearToken();
    navigate('/');
  };

  return (
    <header className="app-header">
      <div className="app-header__inner">
        <Link to="/" className="brand" aria-label="Poll &amp; Survey Builder — home">
          <span className="brand__mark" aria-hidden="true">
            <Vote size={20} strokeWidth={2.25} />
          </span>
          <span className="brand__text h-gradient">PollBuilder</span>
        </Link>
        <nav className="app-nav" aria-label="Primary">
          <NavLink to="/create" className={navLinkClass}>
            Create
          </NavLink>
          {authed ? (
            <>
              <NavLink to="/my-polls" className={navLinkClass}>
                My Polls
              </NavLink>
              {admin && (
                <NavLink to="/admin" className={navLinkClass}>
                  <ShieldCheck size={15} strokeWidth={2.25} aria-hidden="true" />
                  Admin
                </NavLink>
              )}
              <button type="button" className="nav-link nav-link--logout" onClick={logout}>
                <LogOut size={15} strokeWidth={2.25} aria-hidden="true" />
                Log out
              </button>
            </>
          ) : (
            <>
              <NavLink to="/login" className={navLinkClass}>
                Log in
              </NavLink>
              <NavLink to="/register" className="nav-link nav-link--cta">
                Register
              </NavLink>
            </>
          )}
        </nav>
      </div>
    </header>
  );
}

// ── Election Night landing chrome (Phase 18) ───────────────────
// The landing page uses its own dark Tailwind "board" header + footer so the
// not-yet-migrated app pages keep the legacy `Nav`/`Footer` untouched.
const boardNavLink = ({ isActive }: { isActive: boolean }) =>
  [
    'inline-flex items-center gap-1.5 rounded-full px-4 py-2 font-display text-sm font-semibold transition-colors duration-150',
    isActive ? 'bg-panel-2 text-tangerine' : 'text-fg-muted hover:bg-panel-2 hover:text-fg',
  ].join(' ');

function BoardNav() {
  const navigate = useNavigate();
  const { authed, isAdmin: admin } = useAuthStatus();

  const logout = () => {
    clearToken();
    navigate('/');
  };

  return (
    <header className="board sticky top-0 z-20 border-b border-line bg-bg/85 backdrop-blur">
      <div className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-6 py-3">
        <Link to="/" className="inline-flex items-center gap-2.5" aria-label="PollBuilder — home">
          <span className="grid h-9 w-9 place-items-center rounded-xl bg-tangerine text-bg shadow-glow-tangerine">
            <Vote size={20} strokeWidth={2.25} />
          </span>
          <span className="font-display text-xl font-extrabold tracking-tight text-fg">
            PollBuilder
          </span>
        </Link>
        <nav className="flex items-center gap-1" aria-label="Primary">
          <NavLink to="/create" className={boardNavLink}>
            Create
          </NavLink>
          {authed ? (
            <>
              <NavLink to="/my-polls" className={boardNavLink}>
                My Polls
              </NavLink>
              {admin && (
                <NavLink to="/admin" className={boardNavLink}>
                  <ShieldCheck size={15} strokeWidth={2.25} aria-hidden="true" />
                  Admin
                </NavLink>
              )}
              <button
                type="button"
                className="inline-flex items-center gap-1.5 rounded-full px-4 py-2 font-display text-sm font-semibold text-fg-muted transition-colors duration-150 hover:bg-panel-2 hover:text-fg"
                onClick={logout}
              >
                <LogOut size={15} strokeWidth={2.25} aria-hidden="true" />
                Log out
              </button>
            </>
          ) : (
            <>
              <NavLink to="/login" className={boardNavLink}>
                Log in
              </NavLink>
              <NavLink
                to="/register"
                className="inline-flex items-center rounded-full bg-tangerine px-4 py-2 font-display text-sm font-semibold text-bg transition-colors duration-150 hover:bg-amber"
              >
                Register
              </NavLink>
            </>
          )}
        </nav>
      </div>
    </header>
  );
}

const footerCols = [
  {
    h: 'Product',
    links: [
      { label: 'Create a poll', to: '/create' },
      { label: 'Live results', href: '#features' },
      { label: 'Analytics', href: '#features' },
      { label: 'Q&A', href: '#features' },
    ],
  },
  {
    h: 'Question types',
    links: [
      { label: 'Multiple choice', href: '#features' },
      { label: 'Yes / No', href: '#features' },
      { label: 'Rating 1–5', href: '#features' },
      { label: 'Open text', href: '#features' },
    ],
  },
  {
    h: 'Account',
    links: [
      { label: 'Log in', to: '/login' },
      { label: 'Register', to: '/register' },
      { label: 'My polls', to: '/my-polls' },
    ],
  },
  {
    h: 'Project',
    links: [
      { label: 'How it works', href: '#how' },
      { label: 'GitHub', href: 'https://github.com/DK0310/poll-service' },
      { label: 'Live demo', to: '/create' },
    ],
  },
];

function BoardFooter() {
  return (
    <footer className="board border-t border-line bg-panel text-fg-muted">
      <div className="mx-auto max-w-6xl px-6 py-14">
        <div className="grid grid-cols-2 gap-8 md:grid-cols-[1.6fr_repeat(4,1fr)]">
          <div className="col-span-2 md:col-span-1">
            <Link to="/" className="inline-flex items-center gap-2.5">
              <span className="grid h-9 w-9 place-items-center rounded-xl bg-tangerine text-bg shadow-glow-tangerine">
                <Vote size={20} strokeWidth={2.25} />
              </span>
              <span className="font-display text-xl font-extrabold tracking-tight text-fg">
                PollBuilder
              </span>
            </Link>
            <p className="mt-4 max-w-60 text-sm leading-relaxed">
              Real-time polls &amp; surveys with live results. An AMD201 microservices project.
            </p>
          </div>
          {footerCols.map((col) => (
            <div key={col.h}>
              <h4 className="font-display text-sm font-bold text-fg">{col.h}</h4>
              <ul className="mt-3.5 space-y-2">
                {col.links.map((l) => (
                  <li key={l.label}>
                    {'to' in l && l.to ? (
                      <Link to={l.to} className="text-sm transition-colors hover:text-fg">
                        {l.label}
                      </Link>
                    ) : (
                      <a
                        href={l.href}
                        className="text-sm transition-colors hover:text-fg"
                        {...(l.href?.startsWith('http')
                          ? { target: '_blank', rel: 'noreferrer' }
                          : {})}
                      >
                        {l.label}
                      </a>
                    )}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
        <div className="mt-12 flex flex-wrap items-center justify-between gap-3 border-t border-line pt-6 font-mono text-xs">
          <span>Poll &amp; Survey Builder · AMD201</span>
          <span>Live results in real time · © 2026</span>
        </div>
      </div>
    </footer>
  );
}

function NotFound() {
  return (
    <div className="page">
      <div className="card notfound">
        <p className="notfound__code h-gradient">404</p>
        <h1>Page not found</h1>
        <p className="muted">That link didn’t lead anywhere. Let’s get you back.</p>
        <Link to="/create" className="btn">
          Create a poll
        </Link>
      </div>
    </div>
  );
}

function Footer() {
  return (
    <footer className="app-footer">
      <span className="mono">Poll &amp; Survey Builder</span>
      <span aria-hidden="true">·</span>
      <span>Live results in real time</span>
      <span aria-hidden="true">·</span>
      <span>AMD201</span>
    </footer>
  );
}

function Layout() {
  const isLanding = useLocation().pathname === '/';

  return (
    <div className="app-shell">
      {isLanding ? <BoardNav /> : <Nav />}
      <main className={isLanding ? 'lp' : undefined}>
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/create" element={<CreatePollPage />} />
          <Route path="/poll/:code" element={<VotePage />} />
          <Route path="/poll/:code/results" element={<ResultsPage />} />
          <Route path="/poll/:code/analytics" element={<AnalyticsPage />} />
          <Route
            path="/my-polls"
            element={
              <RequireAuth>
                <MyPollsPage />
              </RequireAuth>
            }
          />
          <Route
            path="/admin"
            element={
              <RequireAdmin>
                <AdminDashboardPage />
              </RequireAdmin>
            }
          />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="*" element={<NotFound />} />
        </Routes>
      </main>
      {isLanding ? <BoardFooter /> : <Footer />}
    </div>
  );
}

export default function App() {
  // Wake the free-tier backend as the app loads so the first action isn't stuck on cold start.
  useEffect(() => {
    warmBackend();
  }, []);

  return (
    <BrowserRouter>
      <ToastProvider>
        <Layout />
      </ToastProvider>
    </BrowserRouter>
  );
}
