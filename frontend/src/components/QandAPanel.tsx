import { useState, type FormEvent } from 'react';
import { MessageSquare, ChevronUp, Pin, Send } from 'lucide-react';
import { useQuestions } from '../hooks/useQuestions';

interface QandAPanelProps {
  code: string;
  canModerate?: boolean; // poll owner or admin — may pin/unpin
}

export function QandAPanel({ code, canModerate = false }: QandAPanelProps) {
  const { questions, submit, upvote, pin } = useQuestions(code);
  const [text, setText] = useState('');
  const [busy, setBusy] = useState(false);

  const onSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (!text.trim()) return;
    setBusy(true);
    try {
      await submit(text.trim());
      setText('');
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="board board-panel mt-6 p-7 sm:p-9" aria-label="Audience questions">
      <h2 className="mb-4 flex items-center gap-2 font-display text-lg font-bold text-fg">
        <MessageSquare size={18} strokeWidth={2.25} className="text-tangerine" aria-hidden="true" />
        Q&amp;A
      </h2>

      <form onSubmit={onSubmit} className="mb-4 flex gap-2">
        <input
          type="text"
          className="board-input flex-1"
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder="Ask a question…"
          aria-label="Ask a question"
          disabled={busy}
        />
        <button type="submit" className="board-btn flex-none !px-5" disabled={busy || !text.trim()}>
          <Send size={16} strokeWidth={2.25} aria-hidden="true" />
          Ask
        </button>
      </form>

      {questions.length === 0 ? (
        <p className="text-fg-muted">No questions yet — be the first to ask.</p>
      ) : (
        <ul className="flex flex-col gap-2.5">
          {questions.map((q) => (
            <li
              key={q.id}
              className={[
                'flex items-center gap-4 rounded-xl border px-3.5 py-3',
                q.isPinned
                  ? 'border-transparent bg-tangerine/10 shadow-[inset_0_0_0_1px_var(--color-tangerine)]'
                  : 'border-line bg-panel-2',
              ].join(' ')}
            >
              <button
                type="button"
                className="inline-flex min-h-11 min-w-11 flex-none flex-col items-center gap-0.5 rounded-lg border border-line bg-panel p-1.5 font-mono text-xs text-tangerine transition-colors hover:border-tangerine"
                onClick={() => upvote(q.id)}
                aria-label={`Upvote — ${q.upvotes} ${q.upvotes === 1 ? 'vote' : 'votes'}`}
              >
                <ChevronUp size={16} strokeWidth={2.5} aria-hidden="true" />
                <span className="tabular-nums">{q.upvotes}</span>
              </button>
              <span className="flex-1 text-fg">
                {q.isPinned && (
                  <span className="mr-2 inline-flex items-center gap-1 rounded-full bg-tangerine/20 px-2 py-0.5 align-middle font-display text-[11px] font-semibold text-tangerine">
                    <Pin size={12} strokeWidth={2.5} aria-hidden="true" /> Pinned
                  </span>
                )}
                {q.text}
              </span>
              {canModerate && (
                <button
                  type="button"
                  className="flex-none font-display text-sm text-tangerine hover:underline"
                  onClick={() => pin(q.id)}
                >
                  {q.isPinned ? 'Unpin' : 'Pin'}
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
