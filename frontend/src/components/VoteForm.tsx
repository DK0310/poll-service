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
    <button type="submit" className="board-btn board-btn--block" disabled={isDisabled}>
      {submitting && <Loader2 size={18} strokeWidth={2.25} className="board-spin" aria-hidden="true" />}
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
      <form onSubmit={submitText} className="mt-4 flex flex-col gap-4">
        <div>
          <label htmlFor="answer" className="board-label">
            Your answer
          </label>
          <textarea
            id="answer"
            className="board-input min-h-32 resize-y leading-relaxed"
            value={text}
            onChange={(e) => setText(e.target.value)}
            placeholder="Type your answer…"
            rows={4}
            disabled={disabled}
            required
          />
        </div>
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
      <form onSubmit={handleSubmit} className="mt-4 flex flex-col gap-5">
        <div className="flex gap-2" role="radiogroup" aria-label="Rating from 1 to 5">
          {options.map((opt) => {
            const on = selected === opt.optionIndex;
            return (
              <button
                key={opt.optionIndex}
                type="button"
                role="radio"
                aria-checked={on}
                className={[
                  'min-h-14 flex-1 rounded-xl border font-display text-xl font-semibold transition-all duration-150',
                  on
                    ? 'border-transparent bg-tangerine text-bg shadow-glow-tangerine'
                    : 'border-line bg-panel-2 text-fg hover:-translate-y-0.5 hover:border-tangerine',
                ].join(' ')}
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
      <form onSubmit={handleSubmit} className="mt-4 flex flex-col gap-5">
        <div className="grid grid-cols-2 gap-4" role="radiogroup" aria-label="Yes or No">
          {options.map((opt) => {
            const on = selected === opt.optionIndex;
            return (
              <button
                key={opt.optionIndex}
                type="button"
                role="radio"
                aria-checked={on}
                className={[
                  'min-h-24 rounded-2xl border font-display text-2xl font-semibold transition-all duration-150',
                  on
                    ? 'border-transparent bg-tangerine text-bg shadow-glow-tangerine'
                    : 'border-line bg-panel-2 text-fg hover:-translate-y-0.5 hover:border-tangerine',
                ].join(' ')}
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

  // ── SingleChoice: option rows (native radios) ─────────────
  return (
    <form onSubmit={handleSubmit} className="mt-4 flex flex-col gap-5">
      <div className="flex flex-col gap-2.5" role="radiogroup" aria-label="Answer options">
        {options.map((opt) => {
          const on = selected === opt.optionIndex;
          return (
            <label
              key={opt.optionIndex}
              className={[
                'flex min-h-[52px] cursor-pointer items-center gap-4 rounded-xl border px-4 py-3.5 transition-all duration-150 has-[:focus-visible]:outline has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-teal',
                on
                  ? 'border-transparent bg-tangerine/10 shadow-[inset_0_0_0_2px_var(--color-tangerine)]'
                  : 'border-line bg-panel-2 hover:border-tangerine',
              ].join(' ')}
            >
              <input
                className="sr-only"
                type="radio"
                name="vote"
                value={opt.optionIndex}
                checked={on}
                onChange={() => setSelected(opt.optionIndex)}
                disabled={disabled}
              />
              <span className="flex-1 text-lg text-fg">{opt.text}</span>
              <span
                className={[
                  'grid h-7 w-7 flex-none place-items-center rounded-full border',
                  on ? 'border-transparent bg-tangerine text-bg' : 'border-line text-transparent',
                ].join(' ')}
                aria-hidden="true"
              >
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
