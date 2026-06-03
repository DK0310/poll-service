import { useState, type FormEvent } from 'react';
import type { PollOption } from '../types/poll.types';

interface VoteFormProps {
  type: string; // QuestionType
  options: PollOption[];
  onVote: (optionIndex: number, textAnswer?: string) => void;
  disabled?: boolean;
}

export function VoteForm({ type, options, onVote, disabled }: VoteFormProps) {
  const [selected, setSelected] = useState<number | null>(null);
  const [text, setText] = useState('');

  // ── Open text ─────────────────────────────────────────────
  if (type === 'OpenText') {
    const submitText = (e: FormEvent<HTMLFormElement>) => {
      e.preventDefault();
      if (text.trim()) onVote(0, text.trim());
    };
    return (
      <form onSubmit={submitText} className="vote-form">
        <textarea
          className="text-answer"
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder="Type your answer…"
          rows={4}
          disabled={disabled}
          required
        />
        <button type="submit" className="btn-vote" disabled={disabled || !text.trim()}>
          Submit
        </button>
      </form>
    );
  }

  const handleSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (selected !== null) onVote(selected);
  };

  // ── Rating: a row of number buttons (option index = rating − 1) ──
  if (type === 'Rating') {
    return (
      <form onSubmit={handleSubmit} className="vote-form">
        <div className="rating-row">
          {options.map((opt) => (
            <button
              key={opt.optionIndex}
              type="button"
              className={`rating-btn ${selected === opt.optionIndex ? 'selected' : ''}`}
              onClick={() => setSelected(opt.optionIndex)}
              disabled={disabled}
            >
              {opt.text}
            </button>
          ))}
        </div>
        <button type="submit" className="btn-vote" disabled={disabled || selected === null}>
          Vote
        </button>
      </form>
    );
  }

  // ── SingleChoice / YesNo: radio options ───────────────────
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
