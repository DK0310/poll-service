import { Link } from 'react-router-dom';
import { BarChart3, TrendingUp, ExternalLink, Lock, Trash2 } from 'lucide-react';
import type { PollInfo } from '../types/poll.types';

interface PollCardProps {
  poll: PollInfo;
  onClose: (code: string) => void;
  onDelete: (code: string) => void;
  busy?: boolean;
}

export function PollCard({ poll, onClose, onDelete, busy }: PollCardProps) {
  const optionsText = poll.options.length > 0 ? `${poll.options.length} options` : 'open text';

  return (
    <article className="card poll-card">
      <div className="poll-card__head">
        <h3>{poll.question}</h3>
        <span className={`pill ${poll.isActive ? 'pill--open' : 'pill--closed'}`}>
          {poll.isActive ? 'Open' : 'Closed'}
        </span>
      </div>

      <p className="poll-card__meta mono">/poll/{poll.code} · {optionsText}</p>

      <div className="poll-card__actions">
        <div className="poll-card__nav">
          <Link to={`/poll/${poll.code}/results`} className="poll-action">
            <BarChart3 size={16} strokeWidth={2.25} aria-hidden="true" /> Results
          </Link>
          <Link
            to={`/poll/${poll.code}/analytics`}
            state={{ from: 'my-polls' }}
            className="poll-action"
          >
            <TrendingUp size={16} strokeWidth={2.25} aria-hidden="true" /> Analytics
          </Link>
          <Link to={`/poll/${poll.code}`} className="poll-action">
            <ExternalLink size={16} strokeWidth={2.25} aria-hidden="true" /> Vote
          </Link>
        </div>

        <div className="poll-card__manage">
          {poll.isActive && (
            <button type="button" className="btn-link" onClick={() => onClose(poll.code)} disabled={busy}>
              <Lock size={15} strokeWidth={2.25} aria-hidden="true" /> Close
            </button>
          )}
          <button
            type="button"
            className="btn-link danger"
            onClick={() => onDelete(poll.code)}
            disabled={busy}
          >
            <Trash2 size={15} strokeWidth={2.25} aria-hidden="true" /> Delete
          </button>
        </div>
      </div>
    </article>
  );
}
