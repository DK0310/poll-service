---
name: pollbuilder-frontend
description: Use when building React components, pages, hooks, or integrating with the backend microservices via the API Gateway
---

# Poll Builder — Frontend Patterns Skill

## Overview

The frontend is a React 18 SPA built with TypeScript and Vite. It communicates exclusively with the **API Gateway** — never directly with individual microservices. Axios handles REST API calls. SignalR (via `@microsoft/signalr`) handles real-time vote updates. Components are small and focused. Complex logic (API calls, state management, WebSocket connections) lives in custom hooks.

**Core rule:** Components render UI. Hooks handle logic. All traffic goes through the Gateway.

---

## Project Structure

```
frontend/
├── src/
│   ├── api/
│   │   └── api.ts                    ← Axios instance (→ Gateway)
│   │
│   ├── types/
│   │   └── poll.types.ts             ← TypeScript interfaces for API data
│   │
│   ├── hooks/
│   │   ├── useCreatePoll.ts          ← Poll creation
│   │   ├── usePollInfo.ts            ← Fetch poll by code
│   │   ├── useVote.ts                ← Submit vote
│   │   ├── useLiveResults.ts         ← SignalR + initial results
│   │   └── useMyPolls.ts             ← Fetch creator's polls
│   │
│   ├── components/
│   │   ├── PollForm.tsx              ← Create poll form (question + options)
│   │   ├── VoteForm.tsx              ← Vote selection interface
│   │   ├── LiveBarChart.tsx           ← Animated results bar chart
│   │   ├── PollCard.tsx              ← Poll summary card
│   │   └── ShareLink.tsx             ← Copyable share link
│   │
│   ├── pages/
│   │   ├── CreatePollPage.tsx        ← Poll creation interface
│   │   ├── VotePage.tsx              ← Voting page (by code)
│   │   ├── ResultsPage.tsx           ← Live results page
│   │   ├── MyPollsPage.tsx           ← Creator's poll dashboard
│   │   ├── LoginPage.tsx             ← Login form
│   │   └── RegisterPage.tsx          ← Registration form
│   │
│   └── App.tsx                        ← Router setup
│
├── .env                               ← VITE_API_URL, VITE_HUB_URL
├── vite.config.ts
├── package.json
└── tsconfig.json
```

---

## API Client Setup

**Location:** `frontend/src/api/api.ts`

All REST calls go through this Axios instance, which points to the **Gateway**.

```typescript
import axios from 'axios';

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? 'http://localhost:5000/api',
});

// ── Request interceptor: Inject JWT token ─────────────────────
api.interceptors.request.use(cfg => {
  const token = localStorage.getItem('token');
  if (token) {
    cfg.headers.Authorization = `Bearer ${token}`;
  }
  return cfg;
});

// ── Response interceptor: Handle 401 globally ─────────────────
api.interceptors.response.use(
  response => response,
  error => {
    if (error.response?.status === 401) {
      localStorage.removeItem('token');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

export default api;
```

**Rules:**
- All requests go to the Gateway URL (`VITE_API_URL`)
- The frontend has NO knowledge of individual microservice URLs
- Gateway handles routing to poll-api, vote-api, identity-api

---

## TypeScript Types

**Location:** `frontend/src/types/poll.types.ts`

```typescript
// ── Poll types (from Poll API via Gateway) ──────────────────

export interface PollInfo {
  code: string;
  question: string;
  status: string;           // "Open" | "Closed"
  createdAt: string;        // ISO 8601
  expiresAt: string | null;
  isActive: boolean;
  options: PollOption[];
  url: string;              // "/poll/{code}"
}

export interface PollOption {
  optionIndex: number;
  text: string;
}

export interface CreatePollData {
  question: string;
  options: string[];
  expiryHours?: number;
}

// ── Vote types (from Vote API via Gateway) ──────────────────

export interface VoteData {
  optionIndex: number;
  voterToken: string;
}

export interface VoteResults {
  pollCode: string;
  question: string;
  totalVotes: number;
  isActive: boolean;
  options: OptionResult[];
}

export interface OptionResult {
  optionIndex: number;
  text: string;
  voteCount: number;
  percentage: number;
}

// ── Auth types (from Identity API via Gateway) ──────────────

export interface AuthRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  token: string;
}
```

---

## Custom Hooks

### useCreatePoll — Create a Poll

**Location:** `frontend/src/hooks/useCreatePoll.ts`

```typescript
import { useState } from 'react';
import api from '../api/api';
import type { PollInfo, CreatePollData } from '../types/poll.types';

export function useCreatePoll() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const createPoll = async (data: CreatePollData): Promise<PollInfo | null> => {
    setLoading(true);
    setError(null);

    try {
      const { data: poll } = await api.post<PollInfo>('/polls', data);
      return poll;
    } catch (err: any) {
      setError(err.response?.data?.error ?? 'Failed to create poll');
      return null;
    } finally {
      setLoading(false);
    }
  };

  return { createPoll, loading, error };
}
```

### usePollInfo — Fetch Poll Details

**Location:** `frontend/src/hooks/usePollInfo.ts`

```typescript
import { useEffect, useState } from 'react';
import api from '../api/api';
import type { PollInfo } from '../types/poll.types';

export function usePollInfo(code: string) {
  const [poll, setPoll] = useState<PollInfo | null>(null);
  const [loading, setLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);

  useEffect(() => {
    let cancelled = false;

    api.get<PollInfo>(`/polls/${code}`)
      .then(r => { if (!cancelled) setPoll(r.data); })
      .catch(e => { if (!cancelled && e.response?.status === 404) setNotFound(true); })
      .finally(() => { if (!cancelled) setLoading(false); });

    return () => { cancelled = true; };
  }, [code]);

  return { poll, loading, notFound };
}
```

### useVote — Submit a Vote

**Location:** `frontend/src/hooks/useVote.ts`

```typescript
import { useState } from 'react';
import api from '../api/api';
import type { VoteResults } from '../types/poll.types';

// Generate or retrieve a persistent voter token
function getVoterToken(): string {
  let token = localStorage.getItem('voter_token');
  if (!token) {
    token = crypto.randomUUID();
    localStorage.setItem('voter_token', token);
  }
  return token;
}

export function useVote(pollCode: string) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [hasVoted, setHasVoted] = useState(false);

  const vote = async (optionIndex: number): Promise<VoteResults | null> => {
    setLoading(true);
    setError(null);

    try {
      const { data } = await api.post<VoteResults>(`/polls/${pollCode}/vote`, {
        optionIndex,
        voterToken: getVoterToken(),
      });
      setHasVoted(true);
      return data;
    } catch (err: any) {
      const msg = err.response?.data?.error ?? 'Failed to submit vote';
      if (msg.includes('already voted')) setHasVoted(true);
      setError(msg);
      return null;
    } finally {
      setLoading(false);
    }
  };

  return { vote, loading, error, hasVoted };
}
```

### useLiveResults — SignalR Real-Time Updates

**Location:** `frontend/src/hooks/useLiveResults.ts`

This is the most complex hook — it manages the SignalR connection and updates results in real time.

```typescript
import { useEffect, useState, useRef } from 'react';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import api from '../api/api';
import type { VoteResults } from '../types/poll.types';

const HUB_URL = import.meta.env.VITE_HUB_URL ?? 'http://localhost:5000/hubs/poll';

export function useLiveResults(pollCode: string) {
  const [results, setResults] = useState<VoteResults | null>(null);
  const [loading, setLoading] = useState(true);
  const [connected, setConnected] = useState(false);
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    let cancelled = false;

    // STEP 1: Fetch initial results via REST (through Gateway)
    api.get<VoteResults>(`/polls/${pollCode}/results`)
      .then(r => { if (!cancelled) setResults(r.data); })
      .catch(() => {})
      .finally(() => { if (!cancelled) setLoading(false); });

    // STEP 2: Connect to SignalR hub (through Gateway)
    const connection = new HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    // STEP 3: Listen for live vote updates
    connection.on('ReceiveVoteUpdate', (updated: VoteResults) => {
      if (!cancelled) setResults(updated);
    });

    // STEP 4: Start connection and join poll group
    connection.start()
      .then(() => {
        if (!cancelled) {
          setConnected(true);
          connection.invoke('JoinPollGroup', pollCode);
        }
      })
      .catch(err => console.error('SignalR connection failed:', err));

    // Cleanup: leave group and stop connection
    return () => {
      cancelled = true;
      if (connection.state === 'Connected') {
        connection.invoke('LeavePollGroup', pollCode)
          .finally(() => connection.stop());
      } else {
        connection.stop();
      }
    };
  }, [pollCode]);

  return { results, loading, connected };
}
```

**Install SignalR client:**
```bash
npm install @microsoft/signalr
```

### useMyPolls — Creator's Poll List

**Location:** `frontend/src/hooks/useMyPolls.ts`

```typescript
import { useEffect, useState } from 'react';
import api from '../api/api';
import type { PollInfo } from '../types/poll.types';

export function useMyPolls() {
  const [polls, setPolls] = useState<PollInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    api.get<PollInfo[]>('/polls/my-polls')
      .then(r => { if (!cancelled) setPolls(r.data); })
      .catch(e => {
        if (!cancelled)
          setError(e.response?.data?.error ?? 'Failed to load polls');
      })
      .finally(() => { if (!cancelled) setLoading(false); });

    return () => { cancelled = true; };
  }, []);

  return { polls, loading, error };
}
```

---

## Components

### PollForm — Create Poll Form

**Location:** `frontend/src/components/PollForm.tsx`

```typescript
import { useState } from 'react';

interface PollFormProps {
  onSubmit: (question: string, options: string[], expiryHours?: number) => void;
  disabled?: boolean;
}

export function PollForm({ onSubmit, disabled }: PollFormProps) {
  const [question, setQuestion] = useState('');
  const [options, setOptions] = useState(['', '']);
  const [expiryHours, setExpiryHours] = useState<number | undefined>();

  const addOption = () => {
    if (options.length < 6) setOptions([...options, '']);
  };

  const removeOption = (index: number) => {
    if (options.length > 2) setOptions(options.filter((_, i) => i !== index));
  };

  const updateOption = (index: number, text: string) => {
    setOptions(options.map((o, i) => (i === index ? text : o)));
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit(question, options.filter(o => o.trim()), expiryHours);
  };

  return (
    <form onSubmit={handleSubmit} className="poll-form">
      <div className="form-group">
        <label htmlFor="question">Your Question</label>
        <input
          id="question"
          type="text"
          value={question}
          onChange={e => setQuestion(e.target.value)}
          placeholder="What would you like to ask?"
          disabled={disabled}
          required
        />
      </div>

      <div className="form-group">
        <label>Answer Options</label>
        {options.map((opt, i) => (
          <div key={i} className="option-row">
            <input
              type="text"
              value={opt}
              onChange={e => updateOption(i, e.target.value)}
              placeholder={`Option ${i + 1}`}
              disabled={disabled}
              required
            />
            {options.length > 2 && (
              <button type="button" onClick={() => removeOption(i)} className="btn-remove">
                ✕
              </button>
            )}
          </div>
        ))}
        {options.length < 6 && (
          <button type="button" onClick={addOption} className="btn-add-option" disabled={disabled}>
            + Add Option
          </button>
        )}
      </div>

      <div className="form-group">
        <label htmlFor="expiry">Expiry (optional)</label>
        <select
          id="expiry"
          value={expiryHours ?? ''}
          onChange={e => setExpiryHours(e.target.value ? Number(e.target.value) : undefined)}
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
```

### VoteForm — Vote Selection

**Location:** `frontend/src/components/VoteForm.tsx`

```typescript
import { useState } from 'react';
import type { PollOption } from '../types/poll.types';

interface VoteFormProps {
  options: PollOption[];
  onVote: (optionIndex: number) => void;
  disabled?: boolean;
}

export function VoteForm({ options, onVote, disabled }: VoteFormProps) {
  const [selected, setSelected] = useState<number | null>(null);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (selected !== null) onVote(selected);
  };

  return (
    <form onSubmit={handleSubmit} className="vote-form">
      {options.map(opt => (
        <label key={opt.optionIndex} className={`vote-option ${selected === opt.optionIndex ? 'selected' : ''}`}>
          <input
            type="radio"
            name="vote"
            value={opt.optionIndex}
            checked={selected === opt.optionIndex}
            onChange={() => setSelected(opt.optionIndex)}
            disabled={disabled}
          />
          <span>{opt.text}</span>
        </label>
      ))}
      <button type="submit" className="btn-vote" disabled={disabled || selected === null}>
        Vote
      </button>
    </form>
  );
}
```

### LiveBarChart — Animated Results

**Location:** `frontend/src/components/LiveBarChart.tsx`

```typescript
import type { OptionResult } from '../types/poll.types';

interface LiveBarChartProps {
  options: OptionResult[];
  totalVotes: number;
}

export function LiveBarChart({ options, totalVotes }: LiveBarChartProps) {
  const maxCount = Math.max(...options.map(o => o.voteCount), 1);

  return (
    <div className="bar-chart">
      {options.map(opt => (
        <div key={opt.optionIndex} className="bar-row">
          <span className="bar-label">{opt.text}</span>
          <div className="bar-track">
            <div
              className="bar-fill"
              style={{
                width: `${(opt.voteCount / maxCount) * 100}%`,
                transition: 'width 0.5s ease-out',
              }}
            />
          </div>
          <span className="bar-count">
            {opt.voteCount} ({opt.percentage}%)
          </span>
        </div>
      ))}
      <p className="total-votes">{totalVotes} total votes</p>
    </div>
  );
}
```

### ShareLink — Copyable Link

**Location:** `frontend/src/components/ShareLink.tsx`

```typescript
import { useState } from 'react';

interface ShareLinkProps {
  code: string;
}

export function ShareLink({ code }: ShareLinkProps) {
  const [copied, setCopied] = useState(false);
  const url = `${window.location.origin}/poll/${code}`;

  const copy = async () => {
    await navigator.clipboard.writeText(url);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="share-link">
      <code>{url}</code>
      <button onClick={copy} className="btn-copy">
        {copied ? '✓ Copied!' : 'Copy Link'}
      </button>
    </div>
  );
}
```

---

## Pages

### CreatePollPage

```typescript
import { useState } from 'react';
import { PollForm } from '../components/PollForm';
import { ShareLink } from '../components/ShareLink';
import { useCreatePoll } from '../hooks/useCreatePoll';
import type { PollInfo } from '../types/poll.types';

export function CreatePollPage() {
  const { createPoll, loading, error } = useCreatePoll();
  const [result, setResult] = useState<PollInfo | null>(null);

  const handleSubmit = async (question: string, options: string[], expiryHours?: number) => {
    const poll = await createPoll({ question, options, expiryHours });
    if (poll) setResult(poll);
  };

  return (
    <div className="page">
      <h1>Create a Poll</h1>
      {!result ? (
        <>
          <PollForm onSubmit={handleSubmit} disabled={loading} />
          {error && <p className="error">{error}</p>}
        </>
      ) : (
        <div className="success">
          <h2>Poll Created!</h2>
          <p>{result.question}</p>
          <ShareLink code={result.code} />
        </div>
      )}
    </div>
  );
}
```

### VotePage

```typescript
import { useParams, useNavigate } from 'react-router-dom';
import { usePollInfo } from '../hooks/usePollInfo';
import { useVote } from '../hooks/useVote';
import { VoteForm } from '../components/VoteForm';

export function VotePage() {
  const { code } = useParams<{ code: string }>();
  const navigate = useNavigate();
  const { poll, loading: pollLoading, notFound } = usePollInfo(code!);
  const { vote, loading: voteLoading, error, hasVoted } = useVote(code!);

  if (pollLoading) return <p>Loading…</p>;
  if (notFound) return <p>Poll not found.</p>;
  if (!poll) return null;

  const handleVote = async (optionIndex: number) => {
    const result = await vote(optionIndex);
    if (result) navigate(`/poll/${code}/results`);
  };

  return (
    <div className="page">
      <h1>{poll.question}</h1>
      {!poll.isActive && <p className="closed-banner">This poll is closed</p>}
      {hasVoted ? (
        <p>You have already voted. <a href={`/poll/${code}/results`}>View results</a></p>
      ) : (
        <>
          <VoteForm options={poll.options} onVote={handleVote} disabled={voteLoading || !poll.isActive} />
          {error && <p className="error">{error}</p>}
        </>
      )}
    </div>
  );
}
```

### ResultsPage (Live)

```typescript
import { useParams } from 'react-router-dom';
import { useLiveResults } from '../hooks/useLiveResults';
import { LiveBarChart } from '../components/LiveBarChart';

export function ResultsPage() {
  const { code } = useParams<{ code: string }>();
  const { results, loading, connected } = useLiveResults(code!);

  if (loading) return <p>Loading results…</p>;
  if (!results) return <p>Poll not found.</p>;

  return (
    <div className="page">
      <h1>{results.question}</h1>
      {connected && <span className="live-badge">● Live</span>}
      {!results.isActive && <p className="closed-banner">Poll closed — final results</p>}
      <LiveBarChart options={results.options} totalVotes={results.totalVotes} />
      <p className="share-hint">Share this page — results update in real time!</p>
    </div>
  );
}
```

---

## Routing

**Location:** `frontend/src/App.tsx`

```typescript
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { CreatePollPage } from './pages/CreatePollPage';
import { VotePage } from './pages/VotePage';
import { ResultsPage } from './pages/ResultsPage';
import { MyPollsPage } from './pages/MyPollsPage';
import { LoginPage } from './pages/LoginPage';
import { RegisterPage } from './pages/RegisterPage';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<CreatePollPage />} />
        <Route path="/poll/:code" element={<VotePage />} />
        <Route path="/poll/:code/results" element={<ResultsPage />} />
        <Route path="/my-polls" element={<MyPollsPage />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
      </Routes>
    </BrowserRouter>
  );
}
```

---

## Environment Variables

```env
# frontend/.env
VITE_API_URL=http://localhost:5000/api
VITE_HUB_URL=http://localhost:5000/hubs/poll
```

Both point to the **Gateway** (port 5000). The frontend never directly contacts any microservice.

---

## Common Mistakes

| ❌ Don't | ✅ Do | Why |
|---|---|---|
| Call microservices directly | All calls go through Gateway URL | Service discovery, centralized auth |
| Create multiple axios instances | Import `api` from `src/api/api.ts` | Single instance with interceptors |
| Manage SignalR in components | Use `useLiveResults` hook | Separation of concerns |
| Forget cleanup in `useEffect` | Return cleanup function | Prevents memory leaks |
| Hard-code API URLs | Use `VITE_API_URL` env variable | Different per environment |
| Skip `withAutomaticReconnect()` | Always include for SignalR | Handle network interruptions |
| Forget voter token persistence | Use `localStorage` for token | Same user = same token across refreshes |
| Use `any` types | Define interfaces in `types/` | TypeScript catches bugs at compile time |

---

## Cross-References

- **Backend API endpoints** → `pollbuilder-backend/SKILL.md`
- **System architecture** → `pollbuilder-architecture/SKILL.md`
- **Deploying frontend** → `pollbuilder-devops/SKILL.md`
- **Database schema** → `pollbuilder-database/SKILL.md`
- **Testing** → `pollbuilder-testing/SKILL.md`
