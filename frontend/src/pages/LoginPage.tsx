import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { LogIn, Eye, EyeOff, Loader2 } from 'lucide-react';
import { useAuth } from '../hooks/useAuth';

export function LoginPage() {
  const { login, loading, error } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPw, setShowPw] = useState(false);

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (await login(email, password)) navigate('/my-polls');
  };

  return (
    <div className="page page--narrow">
      <div className="card auth-card">
        <div className="auth-head">
          <span className="auth-badge" aria-hidden="true">
            <LogIn size={24} strokeWidth={2.25} />
          </span>
          <h1>Welcome back</h1>
          <p className="muted">Log in to manage your polls.</p>
        </div>

        <form onSubmit={handleSubmit} className="auth-form">
          <div className="form-group">
            <label htmlFor="email">Email</label>
            <input
              id="email"
              type="email"
              autoComplete="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              disabled={loading}
              required
            />
          </div>
          <div className="form-group">
            <label htmlFor="password">Password</label>
            <div className="pw-wrap">
              <input
                id="password"
                type={showPw ? 'text' : 'password'}
                autoComplete="current-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                disabled={loading}
                required
              />
              <button
                type="button"
                className="pw-toggle"
                onClick={() => setShowPw((s) => !s)}
                aria-label={showPw ? 'Hide password' : 'Show password'}
              >
                {showPw ? <EyeOff size={18} strokeWidth={2.25} /> : <Eye size={18} strokeWidth={2.25} />}
              </button>
            </div>
          </div>

          <button type="submit" className="btn btn--block" disabled={loading}>
            {loading && <Loader2 size={18} strokeWidth={2.25} className="spin" aria-hidden="true" />}
            {loading ? 'Logging in…' : 'Log in'}
          </button>
          {loading && (
            <p className="muted auth-hint">
              First sign-in can take up to a minute while the free-tier server wakes up.
            </p>
          )}
          {error && <p className="error" role="alert">{error}</p>}
        </form>

        <p className="auth-foot">
          No account? <Link to="/register">Create one</Link>
        </p>
      </div>
    </div>
  );
}
