import { useEffect, useState } from 'react';
import { BrowserRouter, Link, Route, Routes, useNavigate } from 'react-router-dom';
import { CreatePollPage } from './pages/CreatePollPage';
import { VotePage } from './pages/VotePage';
import { ResultsPage } from './pages/ResultsPage';
import { AnalyticsPage } from './pages/AnalyticsPage';
import { MyPollsPage } from './pages/MyPollsPage';
import { LoginPage } from './pages/LoginPage';
import { RegisterPage } from './pages/RegisterPage';
import { AUTH_CHANGED, clearToken, isAuthenticated } from './auth/session';

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
      <Link to="/" className="brand">🗳️ Poll &amp; Survey Builder</Link>
      <nav className="app-nav">
        <Link to="/">Create</Link>
        {authed ? (
          <>
            <Link to="/my-polls">My Polls</Link>
            <button type="button" className="btn-link" onClick={logout}>Log out</button>
          </>
        ) : (
          <>
            <Link to="/login">Log in</Link>
            <Link to="/register">Register</Link>
          </>
        )}
      </nav>
    </header>
  );
}

export default function App() {
  return (
    <BrowserRouter>
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
          <Route path="*" element={<p className="page">Page not found.</p>} />
        </Routes>
      </main>
    </BrowserRouter>
  );
}
