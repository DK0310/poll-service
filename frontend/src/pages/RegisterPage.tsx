import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { UserPlus, Eye, EyeOff, Loader2 } from 'lucide-react';
import { useAuth } from '../hooks/useAuth';

export function RegisterPage() {
  const { register, loading, error } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPw, setShowPw] = useState(false);

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (await register(email, password)) navigate('/my-polls');
  };

  return (
    <div className="page page--narrow">
      <div className="card auth-card">
        <div className="auth-head">
          <span className="auth-badge" aria-hidden="true">
            <UserPlus size={24} strokeWidth={2.25} />
          </span>
          <h1>Create an account</h1>
          <p className="muted">Sign up to create and manage polls.</p>
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
            <label htmlFor="password">Password <span className="muted">(min 6 characters)</span></label>
            <div className="pw-wrap">
              <input
                id="password"
                type={showPw ? 'text' : 'password'}
                autoComplete="new-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                minLength={6}
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
            {loading ? 'Creating account…' : 'Create account'}
          </button>
          {error && <p className="error" role="alert">{error}</p>}
        </form>

        <p className="auth-foot">
          Already have an account? <Link to="/login">Log in</Link>
        </p>
      </div>
    </div>
  );
}
