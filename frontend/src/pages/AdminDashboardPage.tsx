import { Link } from 'react-router-dom';
import { ShieldCheck, Lock, Trash2, ArrowUpCircle, ArrowDownCircle } from 'lucide-react';
import { useAdmin } from '../hooks/useAdmin';

export function AdminDashboardPage() {
  const { polls, users, loading, error, busy, closePoll, deletePoll, setRole, deleteUser } = useAdmin();

  if (loading) {
    return (
      <div className="board mx-auto w-full max-w-3xl">
        <div className="board-panel p-7 sm:p-9">
          <div className="mb-6 h-7 w-1/2 animate-pulse rounded-md bg-panel-2" />
          <div className="flex flex-col gap-3">
            {[0, 1, 2].map((i) => (
              <div key={i} className="h-12 animate-pulse rounded-lg bg-panel-2" />
            ))}
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="board mx-auto w-full max-w-3xl">
      <div className="mb-6 flex items-center gap-3">
        <span
          className="grid h-10 w-10 place-items-center rounded-xl bg-tangerine text-on-accent shadow-glow-tangerine"
          aria-hidden="true"
        >
          <ShieldCheck size={22} strokeWidth={2.25} />
        </span>
        <h1 className="font-display text-2xl font-bold tracking-tight text-fg">Admin dashboard</h1>
      </div>

      {error && (
        <p className="mb-4 text-sm text-tangerine" role="alert">
          {error}
        </p>
      )}

      {/* ── All polls ─────────────────────────────────────── */}
      <section className="board-panel p-6 sm:p-7">
        <h2 className="mb-4 font-display text-lg font-bold text-fg">All polls ({polls.length})</h2>
        {polls.length === 0 ? (
          <p className="text-fg-muted">No polls yet.</p>
        ) : (
          <div className="flex flex-col divide-y divide-line">
            {polls.map((p) => (
              <div key={p.code} className="flex flex-wrap items-center gap-x-4 gap-y-2 py-3">
                <div className="flex min-w-0 flex-1 flex-col gap-0.5">
                  <Link
                    to={`/poll/${p.code}/results`}
                    className="truncate font-display font-semibold text-fg hover:text-tangerine"
                  >
                    {p.title ?? p.questions[0]?.text ?? 'Untitled survey'}
                  </Link>
                  <span className="font-mono text-xs text-fg-muted">/poll/{p.code}</span>
                </div>
                <span
                  className={[
                    'flex-none rounded-full px-3 py-1 font-display text-[11px] font-semibold uppercase tracking-wide',
                    p.isActive ? 'bg-tangerine/20 text-tangerine' : 'bg-panel-2 text-fg-muted',
                  ].join(' ')}
                >
                  {p.isActive ? 'Open' : 'Closed'}
                </span>
                <div className="flex flex-none items-center gap-4">
                  {p.isActive && (
                    <button
                      type="button"
                      className="inline-flex items-center gap-1.5 font-display text-sm text-fg-muted transition-colors hover:text-fg disabled:opacity-50"
                      onClick={() => closePoll(p.code)}
                      disabled={busy}
                    >
                      <Lock size={15} strokeWidth={2.25} aria-hidden="true" /> Close
                    </button>
                  )}
                  <button
                    type="button"
                    className="inline-flex items-center gap-1.5 font-display text-sm text-danger transition-colors hover:underline disabled:opacity-50"
                    onClick={() => deletePoll(p.code)}
                    disabled={busy}
                  >
                    <Trash2 size={15} strokeWidth={2.25} aria-hidden="true" /> Delete
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      {/* ── Users ─────────────────────────────────────────── */}
      <section className="board-panel mt-6 p-6 sm:p-7">
        <h2 className="mb-4 font-display text-lg font-bold text-fg">Users ({users.length})</h2>
        <div className="flex flex-col divide-y divide-line">
          {users.map((u) => (
            <div key={u.id} className="flex flex-wrap items-center gap-x-4 gap-y-2 py-3">
              <div className="flex min-w-0 flex-1 flex-col gap-0.5">
                <span className="truncate font-display font-semibold text-fg">{u.email}</span>
                <span className="font-mono text-xs text-fg-muted">
                  joined {new Date(u.createdAt).toLocaleDateString()}
                </span>
              </div>
              <span
                className={[
                  'flex-none rounded-full px-3 py-1 font-display text-[11px] font-semibold uppercase tracking-wide',
                  u.role === 'Admin' ? 'bg-grape/20 text-grape' : 'bg-panel-2 text-fg-muted',
                ].join(' ')}
              >
                {u.role}
              </span>
              <div className="flex flex-none items-center gap-4">
                {u.role === 'Admin' ? (
                  <button
                    type="button"
                    className="inline-flex items-center gap-1.5 font-display text-sm text-fg-muted transition-colors hover:text-fg disabled:opacity-50"
                    onClick={() => setRole(u.id, 'User')}
                    disabled={busy}
                  >
                    <ArrowDownCircle size={15} strokeWidth={2.25} aria-hidden="true" /> Make user
                  </button>
                ) : (
                  <button
                    type="button"
                    className="inline-flex items-center gap-1.5 font-display text-sm text-fg-muted transition-colors hover:text-fg disabled:opacity-50"
                    onClick={() => setRole(u.id, 'Admin')}
                    disabled={busy}
                  >
                    <ArrowUpCircle size={15} strokeWidth={2.25} aria-hidden="true" /> Make admin
                  </button>
                )}
                <button
                  type="button"
                  className="inline-flex items-center gap-1.5 font-display text-sm text-danger transition-colors hover:underline disabled:opacity-50"
                  onClick={() => deleteUser(u.id)}
                  disabled={busy}
                >
                  <Trash2 size={15} strokeWidth={2.25} aria-hidden="true" /> Delete
                </button>
              </div>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}
