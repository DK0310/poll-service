import { useState, type FormEvent } from 'react';
import { Plus, X, Loader2, Trash2 } from 'lucide-react';
import type { CreatePollData, QuestionType } from '../types/poll.types';

interface PollFormProps {
  onSubmit: (data: CreatePollData) => void;
  disabled?: boolean;
}

const MAX_OPTIONS = 6;
const MIN_OPTIONS = 2;
const MAX_QUESTIONS = 20;

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

interface DraftQuestion {
  text: string;
  type: QuestionType;
  options: string[];
}

const blankQuestion = (): DraftQuestion => ({ text: '', type: 'SingleChoice', options: ['', ''] });

export function PollForm({ onSubmit, disabled }: PollFormProps) {
  const [title, setTitle] = useState('');
  const [questions, setQuestions] = useState<DraftQuestion[]>([blankQuestion()]);
  const [expiryHours, setExpiryHours] = useState<number | undefined>();

  const patchQuestion = (qi: number, patch: Partial<DraftQuestion>) =>
    setQuestions((qs) => qs.map((q, i) => (i === qi ? { ...q, ...patch } : q)));

  const addQuestion = () =>
    setQuestions((qs) => (qs.length < MAX_QUESTIONS ? [...qs, blankQuestion()] : qs));
  const removeQuestion = (qi: number) =>
    setQuestions((qs) => (qs.length > 1 ? qs.filter((_, i) => i !== qi) : qs));

  const addOption = (qi: number) =>
    patchQuestion(qi, questions[qi].options.length < MAX_OPTIONS
      ? { options: [...questions[qi].options, ''] }
      : {});
  const removeOption = (qi: number, oi: number) =>
    patchQuestion(qi, questions[qi].options.length > MIN_OPTIONS
      ? { options: questions[qi].options.filter((_, i) => i !== oi) }
      : {});
  const updateOption = (qi: number, oi: number, text: string) =>
    patchQuestion(qi, { options: questions[qi].options.map((o, i) => (i === oi ? text : o)) });

  const handleSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const data: CreatePollData = {
      title: title.trim() || undefined,
      questions: questions.map((q) => ({
        text: q.text.trim(),
        type: q.type,
        // Only SingleChoice needs creator-defined options; YesNo/Rating are auto, OpenText has none.
        options: q.type === 'SingleChoice' ? q.options.map((o) => o.trim()).filter((o) => o) : [],
      })),
      expiryHours,
    };
    onSubmit(data);
  };

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-6">
      <div>
        <label htmlFor="title" className="board-label">
          Survey title (optional)
        </label>
        <input
          id="title"
          type="text"
          className="board-input"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="e.g. Event feedback"
          disabled={disabled}
        />
      </div>

      {questions.map((q, qi) => {
        const needsOptions = q.type === 'SingleChoice';
        const hint = TYPES.find((t) => t.value === q.type)?.hint ?? '';
        return (
          <fieldset key={qi} className="rounded-xl border border-line bg-panel-2/40 p-5">
            <div className="mb-4 flex items-center justify-between">
              <legend className="font-display text-sm font-semibold text-tangerine">
                Question {qi + 1}
              </legend>
              {questions.length > 1 && (
                <button
                  type="button"
                  onClick={() => removeQuestion(qi)}
                  className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-line px-3 py-1.5 text-xs font-medium text-fg-muted transition-colors hover:border-tangerine hover:text-tangerine disabled:cursor-not-allowed disabled:opacity-50"
                  disabled={disabled}
                  aria-label={`Remove question ${qi + 1}`}
                >
                  <Trash2 size={14} strokeWidth={2.25} aria-hidden="true" /> Remove
                </button>
              )}
            </div>

            <div className="flex flex-col gap-5">
              <div>
                <label htmlFor={`question-${qi}`} className="board-label">
                  Question text <span className="text-tangerine" aria-hidden="true">*</span>
                </label>
                <input
                  id={`question-${qi}`}
                  type="text"
                  className="board-input"
                  value={q.text}
                  onChange={(e) => patchQuestion(qi, { text: e.target.value })}
                  placeholder="What would you like to ask?"
                  disabled={disabled}
                  required
                />
              </div>

              <div>
                <span className="board-label" id={`type-label-${qi}`}>
                  Question type
                </span>
                <div className="flex flex-wrap gap-2" role="group" aria-labelledby={`type-label-${qi}`}>
                  {TYPES.map((t) => {
                    const on = q.type === t.value;
                    return (
                      <button
                        key={t.value}
                        type="button"
                        className={[
                          'min-h-10 rounded-full border px-4 py-2 font-display text-sm font-medium transition-colors duration-150 disabled:cursor-not-allowed disabled:opacity-50',
                          on
                            ? 'border-transparent bg-tangerine text-on-accent shadow-glow-tangerine'
                            : 'border-line bg-panel-2 text-fg-muted hover:border-tangerine hover:text-fg',
                        ].join(' ')}
                        aria-pressed={on}
                        onClick={() => patchQuestion(qi, { type: t.value })}
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
                    {q.options.map((opt, oi) => (
                      <div key={oi} className="flex items-center gap-2">
                        <span
                          className="grid h-9 w-9 flex-none place-items-center rounded-lg bg-panel-2 font-mono text-xs tabular-nums text-tangerine"
                          aria-hidden="true"
                        >
                          {String(oi + 1).padStart(2, '0')}
                        </span>
                        <input
                          type="text"
                          className="board-input flex-1"
                          value={opt}
                          onChange={(e) => updateOption(qi, oi, e.target.value)}
                          placeholder={`Option ${oi + 1}`}
                          aria-label={`Question ${qi + 1} option ${oi + 1}`}
                          disabled={disabled}
                          required
                        />
                        {q.options.length > MIN_OPTIONS && (
                          <button
                            type="button"
                            onClick={() => removeOption(qi, oi)}
                            className="grid h-10 w-10 flex-none place-items-center rounded-lg border border-line text-fg-muted transition-colors hover:border-tangerine hover:text-tangerine disabled:cursor-not-allowed disabled:opacity-50"
                            disabled={disabled}
                            aria-label={`Remove option ${oi + 1}`}
                          >
                            <X size={16} strokeWidth={2.25} />
                          </button>
                        )}
                      </div>
                    ))}
                  </div>
                  {q.options.length < MAX_OPTIONS && (
                    <button
                      type="button"
                      onClick={() => addOption(qi)}
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
            </div>
          </fieldset>
        );
      })}

      {questions.length < MAX_QUESTIONS && (
        <button
          type="button"
          onClick={addQuestion}
          className="inline-flex min-h-11 items-center justify-center gap-1.5 rounded-lg border border-dashed border-tangerine/60 px-4 py-2.5 font-display text-sm font-semibold text-tangerine transition-colors hover:bg-tangerine/10 disabled:cursor-not-allowed disabled:opacity-50"
          disabled={disabled}
        >
          <Plus size={18} strokeWidth={2.25} aria-hidden="true" /> Add question
        </button>
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
