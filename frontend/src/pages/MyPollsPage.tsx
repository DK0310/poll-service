import { useState } from 'react';
import { Link } from 'react-router-dom';
import { LogIn, Plus } from 'lucide-react';
import api, { apiErrorMessage } from '../api/api';
import { PollCard } from '../components/PollCard';
import { useMyPolls } from '../hooks/useMyPolls';
import { useToast } from '../components/Toast';
import { isAuthenticated } from '../auth/session';

export function MyPollsPage() {
  const { polls, loading, error, refresh } = useMyPolls();
  const { toast } = useToast();
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  if (!isAuthenticated()) {
    return (
      <div className="page">
        <div className="card empty-state">
          <h1>My polls</h1>
          <p className="muted">Log in to see the polls you’ve created.</p>
          <Link to="/login" className="btn">
            <LogIn size={18} strokeWidth={2.25} aria-hidden="true" /> Log in
          </Link>
        </div>
      </div>
    );
  }

  const closePoll = async (code: string) => {
    setBusy(true);
    setActionError(null);
    try {
      await api.patch(`/polls/${code}/close`);
      await refresh();
      toast('Poll closed');
    } catch (err) {
      const msg = apiErrorMessage(err, 'Failed to close poll');
      setActionError(msg);
      toast(msg, 'error');
    } finally {
      setBusy(false);
    }
  };

  const deletePoll = async (code: string) => {
    setBusy(true);
    setActionError(null);
    try {
      await api.delete(`/polls/${code}`);
      await refresh();
      toast('Poll deleted');
    } catch (err) {
      const msg = apiErrorMessage(err, 'Failed to delete poll');
      setActionError(msg);
      toast(msg, 'error');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="page">
      <h1>My polls</h1>

      {error && <p className="error" role="alert">{error}</p>}
      {actionError && <p className="error" role="alert">{actionError}</p>}

      {loading ? (
        <div className="poll-list">
          <div className="skeleton skeleton--card" />
          <div className="skeleton skeleton--card" />
        </div>
      ) : polls.length === 0 ? (
        <div className="card empty-state">
          <p className="muted">You haven’t created any polls yet.</p>
          <Link to="/create" className="btn">
            <Plus size={18} strokeWidth={2.25} aria-hidden="true" /> Create your first poll
          </Link>
        </div>
      ) : (
        <div className="poll-list">
          {polls.map((poll) => (
            <PollCard
              key={poll.code}
              poll={poll}
              onClose={closePoll}
              onDelete={deletePoll}
              busy={busy}
            />
          ))}
        </div>
      )}
    </div>
  );
}
