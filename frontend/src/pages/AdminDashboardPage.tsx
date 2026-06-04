import { Link } from 'react-router-dom';
import { ShieldCheck, Lock, Trash2, ArrowUpCircle, ArrowDownCircle } from 'lucide-react';
import { useAdmin } from '../hooks/useAdmin';

export function AdminDashboardPage() {
  const { polls, users, loading, error, busy, closePoll, deletePoll, setRole, deleteUser } = useAdmin();

  if (loading) {
    return (
      <div className="page">
        <div className="card">
          <div className="skeleton skeleton--title" />
          <div className="skeleton skeleton--card" />
        </div>
      </div>
    );
  }

  return (
    <div className="page">
      <div className="admin-head">
        <span className="auth-badge" aria-hidden="true">
          <ShieldCheck size={22} strokeWidth={2.25} />
        </span>
        <h1>Admin dashboard</h1>
      </div>

      {error && <p className="error" role="alert">{error}</p>}

      {/* ── All polls ─────────────────────────────────────── */}
      <section className="card">
        <h2 className="chart-card__title">All polls ({polls.length})</h2>
        {polls.length === 0 ? (
          <p className="muted">No polls yet.</p>
        ) : (
          <div className="admin-list">
            {polls.map((p) => (
              <div key={p.code} className="admin-row">
                <div className="admin-row__main">
                  <Link to={`/poll/${p.code}/results`} className="admin-row__title">{p.question}</Link>
                  <span className="admin-row__meta mono">/poll/{p.code}</span>
                </div>
                <span className={`pill ${p.isActive ? 'pill--open' : 'pill--closed'}`}>
                  {p.isActive ? 'Open' : 'Closed'}
                </span>
                <div className="admin-row__actions">
                  {p.isActive && (
                    <button type="button" className="btn-link" onClick={() => closePoll(p.code)} disabled={busy}>
                      <Lock size={15} strokeWidth={2.25} aria-hidden="true" /> Close
                    </button>
                  )}
                  <button type="button" className="btn-link danger" onClick={() => deletePoll(p.code)} disabled={busy}>
                    <Trash2 size={15} strokeWidth={2.25} aria-hidden="true" /> Delete
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      {/* ── Users ─────────────────────────────────────────── */}
      <section className="card admin-users-card">
        <h2 className="chart-card__title">Users ({users.length})</h2>
        <div className="admin-list">
          {users.map((u) => (
            <div key={u.id} className="admin-row">
              <div className="admin-row__main">
                <span className="admin-row__title">{u.email}</span>
                <span className="admin-row__meta mono">joined {new Date(u.createdAt).toLocaleDateString()}</span>
              </div>
              <span className={`pill ${u.role === 'Admin' ? 'pill--open' : 'pill--closed'}`}>{u.role}</span>
              <div className="admin-row__actions">
                {u.role === 'Admin' ? (
                  <button type="button" className="btn-link" onClick={() => setRole(u.id, 'User')} disabled={busy}>
                    <ArrowDownCircle size={15} strokeWidth={2.25} aria-hidden="true" /> Make user
                  </button>
                ) : (
                  <button type="button" className="btn-link" onClick={() => setRole(u.id, 'Admin')} disabled={busy}>
                    <ArrowUpCircle size={15} strokeWidth={2.25} aria-hidden="true" /> Make admin
                  </button>
                )}
                <button type="button" className="btn-link danger" onClick={() => deleteUser(u.id)} disabled={busy}>
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
