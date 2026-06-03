import { useState, type FormEvent } from 'react';
import type { QuestionType } from '../types/poll.types';

interface PollFormProps {
  onSubmit: (question: string, type: QuestionType, options: string[], expiryHours?: number) => void;
  disabled?: boolean;
}

const MAX_OPTIONS = 6;
const MIN_OPTIONS = 2;

export function PollForm({ onSubmit, disabled }: PollFormProps) {
  const [question, setQuestion] = useState('');
  const [type, setType] = useState<QuestionType>('SingleChoice');
  const [options, setOptions] = useState(['', '']);
  const [expiryHours, setExpiryHours] = useState<number | undefined>();

  // Only SingleChoice needs creator-defined options; YesNo/Rating are auto, OpenText has none.
  const needsOptions = type === 'SingleChoice';

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
        <label htmlFor="question">Your Question</label>
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
        <label htmlFor="type">Question Type</label>
        <select
          id="type"
          value={type}
          onChange={(e) => setType(e.target.value as QuestionType)}
          disabled={disabled}
        >
          <option value="SingleChoice">Multiple choice</option>
          <option value="YesNo">Yes / No</option>
          <option value="Rating">Rating (1–5)</option>
          <option value="OpenText">Open text answer</option>
        </select>
      </div>

      {needsOptions && (
        <div className="form-group">
          <label>Answer Options</label>
          {options.map((opt, i) => (
            <div key={i} className="option-row">
              <input
                type="text"
                value={opt}
                onChange={(e) => updateOption(i, e.target.value)}
                placeholder={`Option ${i + 1}`}
                disabled={disabled}
                required
              />
              {options.length > MIN_OPTIONS && (
                <button type="button" onClick={() => removeOption(i)} className="btn-remove"
                  disabled={disabled} aria-label={`Remove option ${i + 1}`}>✕</button>
              )}
            </div>
          ))}
          {options.length < MAX_OPTIONS && (
            <button type="button" onClick={addOption} className="btn-add-option" disabled={disabled}>
              + Add Option
            </button>
          )}
        </div>
      )}

      {!needsOptions && (
        <p className="muted">
          {type === 'YesNo' && 'Voters choose Yes or No.'}
          {type === 'Rating' && 'Voters pick a rating from 1 to 5.'}
          {type === 'OpenText' && 'Voters submit a free-text answer (collected, not tallied).'}
        </p>
      )}

      <div className="form-group">
        <label htmlFor="expiry">Expiry (optional)</label>
        <select
          id="expiry"
          value={expiryHours ?? ''}
          onChange={(e) => setExpiryHours(e.target.value ? Number(e.target.value) : undefined)}
          disabled={disabled}
        >
          <option value="">No expiry</option>
          <option value="1">1 hour</option>
          <option value="24">1 day</option>
          <option value="168">1 week</option>
        </select>
      </div>

      <button type="submit" className="btn-create" disabled={disabled}>
        Create Poll
      </button>
    </form>
  );
}
