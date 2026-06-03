import { useState, type FormEvent } from 'react';
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
    <section className="qanda">
      <h2>Q&amp;A</h2>
      <form onSubmit={onSubmit} className="qanda-form">
        <input
          type="text"
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder="Ask a question…"
          disabled={busy}
        />
        <button type="submit" className="btn-vote" disabled={busy || !text.trim()}>
          Ask
        </button>
      </form>

      {questions.length === 0 ? (
        <p className="muted">No questions yet — be the first to ask.</p>
      ) : (
        <ul className="qanda-list">
          {questions.map((q) => (
            <li key={q.id} className={`qanda-item ${q.isPinned ? 'pinned' : ''}`}>
              <button type="button" className="upvote-btn" onClick={() => upvote(q.id)} aria-label="Upvote">
                ▲ {q.upvotes}
              </button>
              <span className="qanda-text">
                {q.isPinned && <span className="pin-badge">📌 </span>}
                {q.text}
              </span>
              <button type="button" className="btn-link" onClick={() => pin(q.id)}>
                {q.isPinned ? 'Unpin' : 'Pin'}
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
