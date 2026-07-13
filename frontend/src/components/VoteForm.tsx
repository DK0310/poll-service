import { Check } from 'lucide-react';
import type { PollQuestion } from '../types/poll.types';

export interface DraftAnswer {
  optionIndex: number;
  textAnswer?: string;
}

interface VoteFormProps {
  question: PollQuestion;
  value: DraftAnswer | null;
  onChange: (answer: DraftAnswer | null) => void;
  disabled?: boolean;
}

/// Controlled input for ONE survey question. Reports the current answer up via onChange
/// (null = not yet answered). The surrounding SurveyForm owns the submit button.
export function VoteForm({ question, value, onChange, disabled }: VoteFormProps) {
  const { type, options } = question;
  const selected = value?.optionIndex ?? null;
  const choose = (optionIndex: number) => onChange({ optionIndex });

  // ── Open text ─────────────────────────────────────────────
  if (type === 'OpenText') {
    return (
      <div className="mt-3">
        <label htmlFor={`answer-${question.id}`} className="board-label">
          Your answer
        </label>
        <textarea
          id={`answer-${question.id}`}
          className="board-input min-h-32 resize-y leading-relaxed"
          value={value?.textAnswer ?? ''}
          onChange={(e) => onChange(e.target.value.trim() ? { optionIndex: 0, textAnswer: e.target.value } : null)}
          placeholder="Type your answer…"
          rows={4}
          disabled={disabled}
        />
      </div>
    );
  }

  // ── Rating: 1–5 chips ─────────────────────────────────────
  if (type === 'Rating') {
    return (
      <div className="mt-3 flex gap-2" role="radiogroup" aria-label="Rating from 1 to 5">
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
                  ? 'border-transparent bg-tangerine text-on-accent shadow-glow-tangerine'
                  : 'border-line bg-panel-2 text-fg hover:-translate-y-0.5 hover:border-tangerine',
              ].join(' ')}
              onClick={() => choose(opt.optionIndex)}
              disabled={disabled}
            >
              {opt.text}
            </button>
          );
        })}
      </div>
    );
  }

  // ── Yes / No: two blocks ──────────────────────────────────
  if (type === 'YesNo') {
    return (
      <div className="mt-3 grid grid-cols-2 gap-4" role="radiogroup" aria-label="Yes or No">
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
                  ? 'border-transparent bg-tangerine text-on-accent shadow-glow-tangerine'
                  : 'border-line bg-panel-2 text-fg hover:-translate-y-0.5 hover:border-tangerine',
              ].join(' ')}
              onClick={() => choose(opt.optionIndex)}
              disabled={disabled}
            >
              {opt.text}
            </button>
          );
        })}
      </div>
    );
  }

  // ── SingleChoice: option rows (native radios) ─────────────
  return (
    <div className="mt-3 flex flex-col gap-2.5" role="radiogroup" aria-label="Answer options">
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
              name={`vote-${question.id}`}
              value={opt.optionIndex}
              checked={on}
              onChange={() => choose(opt.optionIndex)}
              disabled={disabled}
            />
            <span className="flex-1 text-lg text-fg">{opt.text}</span>
            <span
              className={[
                'grid h-7 w-7 flex-none place-items-center rounded-full border',
                on ? 'border-transparent bg-tangerine text-on-accent' : 'border-line text-transparent',
              ].join(' ')}
              aria-hidden="true"
            >
              {on && <Check size={16} strokeWidth={3} />}
            </span>
          </label>
        );
      })}
    </div>
  );
}
