import { useEffect, useState } from 'react';
import { BrowserRouter, NavLink, Link, Route, Routes, useNavigate } from 'react-router-dom';
import { Vote, LogOut } from 'lucide-react';
import { CreatePollPage } from './pages/CreatePollPage';
import { VotePage } from './pages/VotePage';
import { ResultsPage } from './pages/ResultsPage';
import { AnalyticsPage } from './pages/AnalyticsPage';
import { MyPollsPage } from './pages/MyPollsPage';
import { LoginPage } from './pages/LoginPage';
import { RegisterPage } from './pages/RegisterPage';
import { AUTH_CHANGED, clearToken, isAuthenticated } from './auth/session';

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  isActive ? 'nav-link nav-link--active' : 'nav-link';

function Nav() {
  const navigate = useNavigate();
  const [authed, setAuthed] = useState(isAuthenticated());

  useEffect(() => {
    const sync = () => setAuthed(isAuthenticated());
    window.addEventListener(AUTH_CHANGED, sync);
    return () => window.removeEventListener(AUTH_CHANGED, sync);
  }, []);

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
          <NavLink to="/" className={navLinkClass} end>
            Create
          </NavLink>
          {authed ? (
            <>
              <NavLink to="/my-polls" className={navLinkClass}>
                My Polls
              </NavLink>
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

function NotFound() {
  return (
    <div className="page">
      <div className="card notfound">
        <p className="notfound__code h-gradient">404</p>
        <h1>Page not found</h1>
        <p className="muted">That link didn’t lead anywhere. Let’s get you back.</p>
        <Link to="/" className="btn">
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

export default function App() {
  return (
    <BrowserRouter>
      <div className="app-shell">
        <Nav />
        <main>
          <Routes>
            <Route path="/" element={<CreatePollPage />} />
            <Route path="/poll/:code" element={<VotePage />} />
            <Route path="/poll/:code/results" element={<ResultsPage />} />
            <Route path="/poll/:code/analytics" element={<AnalyticsPage />} />
            <Route path="/my-polls" element={<MyPollsPage />} />
            <Route path="/login" element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />
            <Route path="*" element={<NotFound />} />
          </Routes>
        </main>
        <Footer />
      </div>
    </BrowserRouter>
  );
}
