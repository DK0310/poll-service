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
    <form onSubmit={handleSubmit} className="poll-form">
      <div className="form-group">
        <label htmlFor="question">
          Your question <span className="req" aria-hidden="true">*</span>
        </label>
        <input
          id="question"
          type="text"
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          placeholder="What would you like to ask?"
          disabled={disabled}
          required
        />
      </div>

      <div className="form-group">
        <span className="field-label" id="type-label">Question type</span>
        <div className="seg" role="group" aria-labelledby="type-label">
          {TYPES.map((t) => (
            <button
              key={t.value}
              type="button"
              className={`seg-btn${type === t.value ? ' seg-btn--on' : ''}`}
              aria-pressed={type === t.value}
              onClick={() => setType(t.value)}
              disabled={disabled}
            >
              {t.label}
            </button>
          ))}
        </div>
      </div>

      {needsOptions ? (
        <div className="form-group">
          <span className="field-label">
            Answer options <span className="req" aria-hidden="true">*</span>
          </span>
          {options.map((opt, i) => (
            <div key={i} className="opt-row">
              <span className="opt-num" aria-hidden="true">{String(i + 1).padStart(2, '0')}</span>
              <input
                type="text"
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
                  className="opt-remove"
                  disabled={disabled}
                  aria-label={`Remove option ${i + 1}`}
                >
                  <X size={16} strokeWidth={2.25} />
                </button>
              )}
            </div>
          ))}
          {options.length < MAX_OPTIONS && (
            <button type="button" onClick={addOption} className="opt-add" disabled={disabled}>
              <Plus size={16} strokeWidth={2.25} aria-hidden="true" /> Add option
            </button>
          )}
        </div>
      ) : (
        <p className="type-hint muted">{hint}</p>
      )}

      <div className="form-group">
        <label htmlFor="expiry">Closes (optional)</label>
        <select
          id="expiry"
          value={expiryHours ?? ''}
          onChange={(e) => setExpiryHours(e.target.value ? Number(e.target.value) : undefined)}
          disabled={disabled}
        >
          {EXPIRY.map((o) => (
            <option key={o.value} value={o.value}>{o.label}</option>
          ))}
        </select>
      </div>

      <button type="submit" className="btn btn--block" disabled={disabled}>
        {disabled ? (
          <>
            <Loader2 size={18} strokeWidth={2.25} className="spin" aria-hidden="true" /> Creating…
          </>
        ) : (
          'Create poll'
        )}
      </button>
    </form>
  );
}
