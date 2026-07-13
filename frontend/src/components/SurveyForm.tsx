import { useState, type FormEvent } from 'react';
import { Loader2 } from 'lucide-react';
import { VoteForm, type DraftAnswer } from './VoteForm';
import type { PollQuestion, QuestionAnswer } from '../types/poll.types';

interface SurveyFormProps {
  questions: PollQuestion[];
  onSubmit: (answers: QuestionAnswer[]) => void;
  disabled?: boolean;
  submitting?: boolean;
}

const isComplete = (q: PollQuestion, a: DraftAnswer | null | undefined): boolean => {
  if (!a) return false;
  if (q.type === 'OpenText') return !!a.textAnswer && a.textAnswer.trim().length > 0;
  return a.optionIndex >= 0;
};

/// Renders every survey question and collects the answers, submitting the whole batch once.
/// The submit button stays disabled until every question is answered.
export function SurveyForm({ questions, onSubmit, disabled, submitting }: SurveyFormProps) {
  const [answers, setAnswers] = useState<Record<string, DraftAnswer | null>>({});

  const setAnswer = (questionId: string, answer: DraftAnswer | null) =>
    setAnswers((prev) => ({ ...prev, [questionId]: answer }));

  const allAnswered = questions.every((q) => isComplete(q, answers[q.id]));
  const answeredCount = questions.filter((q) => isComplete(q, answers[q.id])).length;
  const multi = questions.length > 1;

  const handleSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!allAnswered) return;
    const payload: QuestionAnswer[] = questions.map((q) => {
      const a = answers[q.id]!;
      return q.type === 'OpenText'
        ? { questionId: q.id, optionIndex: 0, textAnswer: a.textAnswer!.trim() }
        : { questionId: q.id, optionIndex: a.optionIndex };
    });
    onSubmit(payload);
  };

  return (
    <form onSubmit={handleSubmit} className="mt-4 flex flex-col gap-7">
      {questions.map((q, i) => (
        <div key={q.id} className={multi ? 'rounded-xl border border-line bg-panel-2/40 p-5' : ''}>
          <h2 className="font-display text-lg font-semibold text-fg">
            {multi && (
              <span className="mr-2 font-mono text-sm text-tangerine tabular-nums">
                {String(i + 1).padStart(2, '0')}
              </span>
            )}
            {q.text}
          </h2>
          <VoteForm
            question={q}
            value={answers[q.id] ?? null}
            onChange={(a) => setAnswer(q.id, a)}
            disabled={disabled}
          />
        </div>
      ))}

      {multi && (
        <p className="text-sm text-fg-muted" aria-live="polite">
          {answeredCount} of {questions.length} answered
        </p>
      )}

      <button type="submit" className="board-btn board-btn--block" disabled={!!disabled || !allAnswered || !!submitting}>
        {submitting && <Loader2 size={18} strokeWidth={2.25} className="board-spin" aria-hidden="true" />}
        {submitting ? 'Submitting…' : multi ? 'Submit answers' : 'Cast your vote'}
      </button>
    </form>
  );
}
