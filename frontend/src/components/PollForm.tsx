import { useState, type FormEvent } from 'react';
import { Plus, X, Loader2 } from 'lucide-react';
import type { QuestionType } from '../types/poll.types';

interface PollFormProps {
  onSubmit: (question: string, type: QuestionType, options: string[], expiryHours?: number) => void;
  disabled?: boolean;
}

const MAX_OPTIONS = 6;
const MIN_OPTIONS = 2;

const TYPES: { value: QuestionType; label: string; hint: string }[] = [
  { value: 'SingleChoice', label: 'Multiple choice', hint: '' },
  { value: 'YesNo', label: 'Yes / No', hint: 'Voters choose Yes or No.' },
  { value: 'Rating', label: 'Rating 1–5', hint: 'Voters pick a rating from 1 to 5.' },
  { value: 'OpenText', label: 'Open text', hint: 'Voters submit a free-text answer (collected, not tallied).' },
];

const EXPIRY = [
  { value: '', label: 'No expiry' },
  { value: '1', label: '1 hour' },
  { value: '24', label: '1 day' },
  { value: '168', label: '1 week' },
];

export function PollForm({ onSubmit, disabled }: PollFormProps) {
  const [question, setQuestion] = useState('');
  const [type, setType] = useState<QuestionType>('SingleChoice');
  const [options, setOptions] = useState(['', '']);
  const [expiryHours, setExpiryHours] = useState<number | undefined>();

  // Only SingleChoice needs creator-defined options; YesNo/Rating are auto, OpenText has none.
  const needsOptions = type === 'SingleChoice';
  const hint = TYPES.find((t) => t.value === type)?.hint ?? '';

  const addOption = () => {
    if (options.length < MAX_OPTIONS) setOptions([...options, '']);
  };
  const removeOption = (index: number) => {
    if (options.length > MIN_OPTIONS) setOptions(options.filter((_, i) => i !== index));
  };
  const updateOption = (index: number, text: string) => {
    setOptions(options.map((o, i) => (i === index ? text : o)));
  };

  const handleSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const opts = needsOptions ? options.map((o) => o.trim()).filter((o) => o) : [];
    onSubmit(question, type, opts, expiryHours);
  };

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-6">
      <div>
        <label htmlFor="question" className="board-label">
          Your question <span className="text-tangerine" aria-hidden="true">*</span>
        </label>
        <input
          id="question"
          type="text"
          className="board-input"
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          placeholder="What would you like to ask?"
          disabled={disabled}
          required
        />
      </div>

      <div>
        <span className="board-label" id="type-label">
          Question type
        </span>
        <div className="flex flex-wrap gap-2" role="group" aria-labelledby="type-label">
          {TYPES.map((t) => {
            const on = type === t.value;
            return (
              <button
                key={t.value}
                type="button"
                className={[
                  'min-h-10 rounded-full border px-4 py-2 font-display text-sm font-medium transition-colors duration-150 disabled:cursor-not-allowed disabled:opacity-50',
                  on
                    ? 'border-transparent bg-tangerine text-bg shadow-glow-tangerine'
                    : 'border-line bg-panel-2 text-fg-muted hover:border-tangerine hover:text-fg',
                ].join(' ')}
                aria-pressed={on}
                onClick={() => setType(t.value)}
                disabled={disabled}
              >
                {t.label}
              </button>
            );
          })}
        </div>
      </div>

      {needsOptions ? (
        <div>
          <span className="board-label">
            Answer options <span className="text-tangerine" aria-hidden="true">*</span>
          </span>
          <div className="flex flex-col gap-2">
            {options.map((opt, i) => (
              <div key={i} className="flex items-center gap-2">
                <span
                  className="grid h-9 w-9 flex-none place-items-center rounded-lg bg-panel-2 font-mono text-xs tabular-nums text-tangerine"
                  aria-hidden="true"
                >
                  {String(i + 1).padStart(2, '0')}
                </span>
                <input
                  type="text"
                  className="board-input flex-1"
                  value={opt}
                  onChange={(e) => updateOption(i, e.target.value)}
                  placeholder={`Option ${i + 1}`}
                  aria-label={`Option ${i + 1}`}
                  disabled={disabled}
                  required
                />
                {options.length > MIN_OPTIONS && (
                  <button
                    type="button"
                    onClick={() => removeOption(i)}
                    className="grid h-10 w-10 flex-none place-items-center rounded-lg border border-line text-fg-muted transition-colors hover:border-tangerine hover:text-tangerine disabled:cursor-not-allowed disabled:opacity-50"
                    disabled={disabled}
                    aria-label={`Remove option ${i + 1}`}
                  >
                    <X size={16} strokeWidth={2.25} />
                  </button>
                )}
              </div>
            ))}
          </div>
          {options.length < MAX_OPTIONS && (
            <button
              type="button"
              onClick={addOption}
              className="mt-2 inline-flex min-h-10 items-center gap-1.5 rounded-lg border border-dashed border-tangerine/60 px-4 py-2 font-display text-sm font-medium text-tangerine transition-colors hover:bg-tangerine/10 disabled:cursor-not-allowed disabled:opacity-50"
              disabled={disabled}
            >
              <Plus size={16} strokeWidth={2.25} aria-hidden="true" /> Add option
            </button>
          )}
        </div>
      ) : (
        <p className="-mt-2 text-sm text-fg-muted">{hint}</p>
      )}

      <div>
        <label htmlFor="expiry" className="board-label">
          Closes (optional)
        </label>
        <select
          id="expiry"
          className="board-input"
          value={expiryHours ?? ''}
          onChange={(e) => setExpiryHours(e.target.value ? Number(e.target.value) : undefined)}
          disabled={disabled}
        >
          {EXPIRY.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      </div>

      <button type="submit" className="board-btn board-btn--block" disabled={disabled}>
        {disabled ? (
          <>
            <Loader2 size={18} strokeWidth={2.25} className="board-spin" aria-hidden="true" /> Creating…
          </>
        ) : (
          'Create poll'
        )}
      </button>
    </form>
  );
}
