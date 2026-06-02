import { useState, type FormEvent } from 'react';
import type { PollOption } from '../types/poll.types';

interface VoteFormProps {
  options: PollOption[];
  onVote: (optionIndex: number) => void;
  disabled?: boolean;
}

export function VoteForm({ options, onVote, disabled }: VoteFormProps) {
  const [selected, setSelected] = useState<number | null>(null);

  const handleSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (selected !== null) onVote(selected);
  };

  return (
    <form onSubmit={handleSubmit} className="vote-form">
      {options.map((opt) => (
        <label
          key={opt.optionIndex}
          className={`vote-option ${selected === opt.optionIndex ? 'selected' : ''}`}
        >
          <input
            type="radio"
            name="vote"
            value={opt.optionIndex}
            checked={selected === opt.optionIndex}
            onChange={() => setSelected(opt.optionIndex)}
            disabled={disabled}
          />
          <span>{opt.text}</span>
        </label>
      ))}
      <button type="submit" className="btn-vote" disabled={disabled || selected === null}>
        Vote
      </button>
    </form>
  );
}
