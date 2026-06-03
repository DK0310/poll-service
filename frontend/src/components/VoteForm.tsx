import { useState, type FormEvent } from 'react';
import { Check, Loader2 } from 'lucide-react';
import type { PollOption } from '../types/poll.types';

interface VoteFormProps {
  type: string; // QuestionType
  options: PollOption[];
  onVote: (optionIndex: number, textAnswer?: string) => void;
  disabled?: boolean;
  submitting?: boolean;
}

export function VoteForm({ type, options, onVote, disabled, submitting }: VoteFormProps) {
  const [selected, setSelected] = useState<number | null>(null);
  const [text, setText] = useState('');

  const renderSubmit = (label: string, isDisabled: boolean) => (
    <button type="submit" className="btn btn--block" disabled={isDisabled}>
      {submitting && <Loader2 size={18} strokeWidth={2.25} className="spin" aria-hidden="true" />}
      {submitting ? 'Submitting…' : label}
    </button>
  );

  // ── Open text ─────────────────────────────────────────────
  if (type === 'OpenText') {
    const submitText = (e: FormEvent<HTMLFormElement>) => {
      e.preventDefault();
      if (text.trim()) onVote(0, text.trim());
    };
    return (
      <form onSubmit={submitText} className="vote-form">
        <label htmlFor="answer" className="field-label">Your answer</label>
        <textarea
          id="answer"
          className="text-answer"
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder="Type your answer…"
          rows={4}
          disabled={disabled}
          required
        />
        {renderSubmit('Submit answer', !!disabled || !text.trim())}
      </form>
    );
  }

  const handleSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (selected !== null) onVote(selected);
  };

  // ── Rating: 1–5 chips ─────────────────────────────────────
  if (type === 'Rating') {
    return (
      <form onSubmit={handleSubmit} className="vote-form">
        <div className="rating-row" role="radiogroup" aria-label="Rating from 1 to 5">
          {options.map((opt) => {
            const on = selected === opt.optionIndex;
            return (
              <button
                key={opt.optionIndex}
                type="button"
                role="radio"
                aria-checked={on}
                className={`rating-chip${on ? ' rating-chip--on' : ''}`}
                onClick={() => setSelected(opt.optionIndex)}
                disabled={disabled}
              >
                {opt.text}
              </button>
            );
          })}
        </div>
        {renderSubmit('Cast your vote', !!disabled || selected === null)}
      </form>
    );
  }

  // ── Yes / No: two blocks ──────────────────────────────────
  if (type === 'YesNo') {
    return (
      <form onSubmit={handleSubmit} className="vote-form">
        <div className="vote-yesno" role="radiogroup" aria-label="Yes or No">
          {options.map((opt) => {
            const on = selected === opt.optionIndex;
            return (
              <button
                key={opt.optionIndex}
                type="button"
                role="radio"
                aria-checked={on}
                className={`yesno-block${on ? ' yesno-block--on' : ''}`}
                onClick={() => setSelected(opt.optionIndex)}
                disabled={disabled}
              >
                {opt.text}
              </button>
            );
          })}
        </div>
        {renderSubmit('Cast your vote', !!disabled || selected === null)}
      </form>
    );
  }

  // ── SingleChoice: glass option rows (native radios) ───────
  return (
    <form onSubmit={handleSubmit} className="vote-form">
      <div className="vote-options" role="radiogroup" aria-label="Answer options">
        {options.map((opt) => {
          const on = selected === opt.optionIndex;
          return (
            <label key={opt.optionIndex} className={`vopt${on ? ' vopt--on' : ''}`}>
              <input
                className="sr-only"
                type="radio"
                name="vote"
                value={opt.optionIndex}
                checked={on}
                onChange={() => setSelected(opt.optionIndex)}
                disabled={disabled}
              />
              <span className="vopt__text">{opt.text}</span>
              <span className="check" aria-hidden="true">
                {on && <Check size={16} strokeWidth={3} />}
              </span>
            </label>
          );
        })}
      </div>
      {renderSubmit('Cast your vote', !!disabled || selected === null)}
    </form>
  );
}
