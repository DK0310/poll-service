// Shapes returned by the backend (camelCase JSON), reached via the Gateway.

export interface PollOption {
  optionIndex: number;
  text: string;
}

export type QuestionType = 'SingleChoice' | 'YesNo' | 'Rating' | 'OpenText';

export interface PollInfo {
  code: string;
  question: string;
  type: string; // QuestionType
  status: string; // "Open" | "Closed"
  createdAt: string; // ISO 8601
  expiresAt: string | null;
  isActive: boolean;
  creatorId: string | null; // owner id — for ownership-gated UI (analytics link, pin)
  options: PollOption[];
  url: string; // "/poll/{code}"
}

export interface CreatePollData {
  question: string;
  type: QuestionType;
  options: string[];
  expiryHours?: number;
}

export interface OptionResult {
  optionIndex: number;
  text: string;
  voteCount: number;
  percentage: number;
}

export interface VoteResults {
  pollCode: string;
  question: string;
  type: string; // QuestionType
  totalVotes: number;
  isActive: boolean;
  options: OptionResult[];
  textAnswers: string[];
}

// ── Auth (Identity API via Gateway) ─────────────────────────
export interface AuthResponse {
  token: string;
}

// ── Admin (admin-only, via Gateway) ─────────────────────────
export interface AdminUser {
  id: string;
  email: string;
  role: string; // "User" | "Admin"
  createdAt: string;
}

// ── Analytics (Vote API via Gateway) ────────────────────────
export interface TimeBucket {
  minute: string; // ISO 8601, truncated to the minute (UTC)
  count: number;
}

export interface TopOption {
  optionIndex: number;
  text: string;
  voteCount: number;
}

export interface Analytics {
  pollCode: string;
  question: string;
  totalVotes: number;
  topOption: TopOption | null;
  peakMinute: TimeBucket | null;
  timeline: TimeBucket[];
}

// ── Q&A (Vote API via Gateway + SignalR) ────────────────────
export interface Question {
  id: string;
  text: string;
  upvotes: number;
  isPinned: boolean;
  createdAt: string;
}
