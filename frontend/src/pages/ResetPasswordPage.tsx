import { useState, type FormEvent } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { KeyRound, Eye, EyeOff, Loader2 } from 'lucide-react';
import { useAuth } from '../hooks/useAuth';
import { useToast } from '../components/Toast';

// Step 2 of password reset: enter the emailed code + a new password. Email is prefilled from
// the /forgot-password hand-off but stays editable so the page also works if reached directly.
export function ResetPasswordPage() {
  const { resetPassword, loading, error } = useAuth();
  const navigate = useNavigate();
  const { toast } = useToast();
  const prefill = (useLocation().state as { email?: string } | null)?.email ?? '';
  const [email, setEmail] = useState(prefill);
  const [code, setCode] = useState('');
  const [password, setPassword] = useState('');
  const [showPw, setShowPw] = useState(false);

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (await resetPassword(email, code, password)) {
      toast('Password updated — please log in.');
      navigate('/login');
    }
  };

  return (
    <div className="board mx-auto w-full max-w-sm">
      <div className="board-panel p-7 sm:p-8">
        <div className="mb-7 flex flex-col items-center text-center">
          <span
            className="mb-3 grid h-12 w-12 place-items-center rounded-xl bg-tangerine text-on-accent shadow-glow-tangerine"
            aria-hidden="true"
          >
            <KeyRound size={24} strokeWidth={2.25} />
          </span>
          <h1 className="font-display text-2xl font-bold tracking-tight text-fg">Choose a new password</h1>
          <p className="mt-1 text-fg-muted">Enter the code we emailed you.</p>
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
          <div>
            <label htmlFor="code" className="board-label">
              Reset code
            </label>
            <input
              id="code"
              inputMode="numeric"
              autoComplete="one-time-code"
              className="board-input tracking-[0.5em]"
              value={code}
              onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
              maxLength={6}
              disabled={loading}
              required
            />
          </div>
          <div>
            <label htmlFor="password" className="board-label">
              New password{' '}
              <span className="font-body normal-case tracking-normal text-fg-faint">(min 6 characters)</span>
            </label>
            <div className="relative">
              <input
                id="password"
                type={showPw ? 'text' : 'password'}
                className="board-input pr-12"
                autoComplete="new-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                minLength={6}
                disabled={loading}
                required
              />
              <button
                type="button"
                className="absolute right-1.5 top-1/2 grid h-9 w-9 -translate-y-1/2 place-items-center rounded-lg text-fg-muted transition-colors hover:text-fg"
                onClick={() => setShowPw((s) => !s)}
                aria-label={showPw ? 'Hide password' : 'Show password'}
              >
                {showPw ? <EyeOff size={18} strokeWidth={2.25} /> : <Eye size={18} strokeWidth={2.25} />}
              </button>
            </div>
          </div>

          <button
            type="submit"
            className="board-btn board-btn--block mt-1"
            disabled={loading || code.length < 6}
          >
            {loading && <Loader2 size={18} strokeWidth={2.25} className="board-spin" aria-hidden="true" />}
            {loading ? 'Updating…' : 'Update password'}
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
