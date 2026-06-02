import { Link } from 'react-router-dom';
import type { PollInfo } from '../types/poll.types';

interface PollCardProps {
  poll: PollInfo;
  onClose: (code: string) => void;
  onDelete: (code: string) => void;
  busy?: boolean;
}

export function PollCard({ poll, onClose, onDelete, busy }: PollCardProps) {
  return (
    <div className="poll-card">
      <div className="poll-card-head">
        <h3>{poll.question}</h3>
        <span className={`status-pill ${poll.isActive ? 'open' : 'closed'}`}>
          {poll.isActive ? 'Open' : 'Closed'}
        </span>
      </div>
      <p className="poll-card-meta">
        <code>/poll/{poll.code}</code> · {poll.options.length} options
      </p>
      <div className="poll-card-actions">
        <Link to={`/poll/${poll.code}/results`}>Results</Link>
        <Link to={`/poll/${poll.code}`}>Vote</Link>
        {poll.isActive && (
          <button type="button" className="btn-link" onClick={() => onClose(poll.code)} disabled={busy}>
            Close
          </button>
        )}
        <button type="button" className="btn-link danger" onClick={() => onDelete(poll.code)} disabled={busy}>
          Delete
        </button>
      </div>
    </div>
  );
}
