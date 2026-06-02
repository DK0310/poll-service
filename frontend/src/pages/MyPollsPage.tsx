import { useState } from 'react';
import { Link } from 'react-router-dom';
import api, { apiErrorMessage } from '../api/api';
import { PollCard } from '../components/PollCard';
import { useMyPolls } from '../hooks/useMyPolls';
import { isAuthenticated } from '../auth/session';

export function MyPollsPage() {
  const { polls, loading, error, refresh } = useMyPolls();
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  if (!isAuthenticated()) {
    return (
      <div className="page">
        <h1>My Polls</h1>
        <p>Please <Link to="/login">log in</Link> to see the polls you've created.</p>
      </div>
    );
  }

  const closePoll = async (code: string) => {
    setBusy(true);
    setActionError(null);
    try {
      await api.patch(`/polls/${code}/close`);
      await refresh();
    } catch (err) {
      setActionError(apiErrorMessage(err, 'Failed to close poll'));
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
    } catch (err) {
      setActionError(apiErrorMessage(err, 'Failed to delete poll'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="page">
      <h1>My Polls</h1>
      {loading && <p>Loading…</p>}
      {error && <p className="error">{error}</p>}
      {actionError && <p className="error">{actionError}</p>}
      {!loading && polls.length === 0 && (
        <p>You haven't created any polls yet. <Link to="/">Create one →</Link></p>
      )}
      {polls.map((poll) => (
        <PollCard key={poll.code} poll={poll} onClose={closePoll} onDelete={deletePoll} busy={busy} />
      ))}
    </div>
  );
}
