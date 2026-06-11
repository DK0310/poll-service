import { useEffect } from 'react';
import { BrowserRouter, NavLink, Link, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import { Vote, LogOut, ShieldCheck, Sun, Moon } from 'lucide-react';
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
import { useTheme } from './hooks/useTheme';
import { warmBackend } from './api/warmup';
import { clearToken } from './auth/session';
import { ToastProvider } from './components/Toast';

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  isActive ? 'nav-link nav-link--active' : 'nav-link';

function Nav() {
  const navigate = useNavigate();
  const { authed, isAdmin: admin } = useAuthStatus();
  const { theme, toggle } = useTheme();

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
          <button
            type="button"
            className="nav-link theme-toggle"
            onClick={toggle}
            aria-label={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
          >
            {theme === 'dark' ? (
              <Sun size={16} strokeWidth={2.25} aria-hidden="true" />
            ) : (
              <Moon size={16} strokeWidth={2.25} aria-hidden="true" />
            )}
          </button>
        </nav>
      </div>
    </header>
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

// Rich marketing footer — shown only on the landing page (/).
function LandingFooter() {
  return (
    <footer className="lp-foot">
      <div className="lp-wrap">
        <div className="foot-grid">
          <div>
            <Link to="/" className="brand">
              <span className="brand__mark" aria-hidden="true">
                <Vote size={20} strokeWidth={2.25} />
              </span>
              <span className="brand__text">PollBuilder</span>
            </Link>
            <p style={{ marginTop: 14, fontSize: 13, color: '#8a8aa6', maxWidth: 240 }}>
              Real-time polls &amp; surveys with live results. An AMD201 microservices project.
            </p>
          </div>
          <div>
            <h4>Product</h4>
            <Link to="/create">Create a poll</Link>
            <a href="#features">Live results</a>
            <a href="#features">Analytics</a>
            <a href="#features">Q&amp;A</a>
          </div>
          <div>
            <h4>Question types</h4>
            <a href="#features">Multiple choice</a>
            <a href="#features">Yes / No</a>
            <a href="#features">Rating 1–5</a>
            <a href="#features">Open text</a>
          </div>
          <div>
            <h4>Account</h4>
            <Link to="/login">Log in</Link>
            <Link to="/register">Register</Link>
            <Link to="/my-polls">My polls</Link>
          </div>
          <div>
            <h4>Project</h4>
            <a href="#how">How it works</a>
            <a href="https://github.com/DK0310/poll-service" target="_blank" rel="noreferrer">GitHub</a>
            <Link to="/create">Live demo</Link>
          </div>
        </div>
        <div className="lp-foot__legal">
          <span className="mono">Poll &amp; Survey Builder · AMD201</span>
          <span>Live results in real time · © 2026</span>
        </div>
      </div>
    </footer>
  );
}

function Layout() {
  const isLanding = useLocation().pathname === '/';

  return (
    <div className="app-shell">
      <Nav />
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
      {isLanding ? <LandingFooter /> : <Footer />}
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
