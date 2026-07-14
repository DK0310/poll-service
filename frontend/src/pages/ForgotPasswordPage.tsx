import { useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Mail, Loader2 } from 'lucide-react';
import { useAuth } from '../hooks/useAuth';

// Step 1 of password reset: request a code. Always reports success (no account enumeration),
// then hands off to /reset-password carrying the email.
export function ForgotPasswordPage() {
  const { forgotPassword, loading, error } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('');

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (await forgotPassword(email)) navigate('/reset-password', { state: { email } });
  };

  return (
    <div className="board mx-auto w-full max-w-sm">
      <div className="board-panel p-7 sm:p-8">
        <div className="mb-7 flex flex-col items-center text-center">
          <span
            className="mb-3 grid h-12 w-12 place-items-center rounded-xl bg-tangerine text-on-accent shadow-glow-tangerine"
            aria-hidden="true"
          >
            <Mail size={24} strokeWidth={2.25} />
          </span>
          <h1 className="font-display text-2xl font-bold tracking-tight text-fg">Reset your password</h1>
          <p className="mt-1 text-fg-muted">Enter your email and we’ll send you a code.</p>
        </div>

        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <div>
            <label htmlFor="email" className="board-label">
              Email
            </label>
            <input
              id="email"
              type="email"
              className="board-input"
              autoComplete="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              disabled={loading}
              required
            />
          </div>

          <button type="submit" className="board-btn board-btn--block mt-1" disabled={loading}>
            {loading && <Loader2 size={18} strokeWidth={2.25} className="board-spin" aria-hidden="true" />}
            {loading ? 'Sending…' : 'Send reset code'}
          </button>
          {error && (
            <p className="text-sm text-tangerine" role="alert">
              {error}
            </p>
          )}
        </form>

        <p className="mt-6 text-center text-sm text-fg-muted">
          <Link to="/login" className="font-semibold text-tangerine hover:underline">
            Back to log in
          </Link>
        </p>
      </div>
    </div>
  );
}
