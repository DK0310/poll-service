import { useState, type FormEvent } from 'react';
import { MessageSquare, ChevronUp, Pin, Send } from 'lucide-react';
import { useQuestions } from '../hooks/useQuestions';

interface QandAPanelProps {
  code: string;
}

export function QandAPanel({ code }: QandAPanelProps) {
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
    <section className="qanda card" aria-label="Audience questions">
      <h2 className="qanda__head">
        <MessageSquare size={18} strokeWidth={2.25} aria-hidden="true" />
        Q&amp;A
      </h2>

      <form onSubmit={onSubmit} className="qanda-form">
        <input
          type="text"
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder="Ask a question…"
          aria-label="Ask a question"
          disabled={busy}
        />
        <button type="submit" className="btn qanda-form__send" disabled={busy || !text.trim()}>
          <Send size={16} strokeWidth={2.25} aria-hidden="true" />
          Ask
        </button>
      </form>

      {questions.length === 0 ? (
        <p className="muted">No questions yet — be the first to ask.</p>
      ) : (
        <ul className="qanda-list">
          {questions.map((q) => (
            <li key={q.id} className={`qanda-item${q.isPinned ? ' qanda-item--pinned' : ''}`}>
              <button
                type="button"
                className="qanda__upvote"
                onClick={() => upvote(q.id)}
                aria-label={`Upvote — ${q.upvotes} ${q.upvotes === 1 ? 'vote' : 'votes'}`}
              >
                <ChevronUp size={16} strokeWidth={2.5} aria-hidden="true" />
                <span className="tnum">{q.upvotes}</span>
              </button>
              <span className="qanda-text">
                {q.isPinned && (
                  <span className="pin-flag">
                    <Pin size={12} strokeWidth={2.5} aria-hidden="true" /> Pinned
                  </span>
                )}
                {q.text}
              </span>
              <button type="button" className="btn-link qanda__pin" onClick={() => pin(q.id)}>
                {q.isPinned ? 'Unpin' : 'Pin'}
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
