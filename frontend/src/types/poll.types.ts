// Shapes returned by the backend (camelCase JSON), reached via the Gateway.

export interface PollOption {
  optionIndex: number;
  text: string;
}

export type QuestionType = 'SingleChoice' | 'YesNo' | 'Rating' | 'OpenText';

// One survey question within a poll.
export interface PollQuestion {
  id: string;
  questionIndex: number;
  text: string;
  type: string; // QuestionType
  options: PollOption[];
}

export interface PollInfo {
  code: string;
  title: string | null; // optional survey title
  status: string; // "Open" | "Closed"
  createdAt: string; // ISO 8601
  expiresAt: string | null;
  isActive: boolean;
  creatorId: string | null; // owner id — for ownership-gated UI (analytics link, pin)
  questions: PollQuestion[];
  url: string; // "/poll/{code}"
}

export interface CreateQuestionData {
  text: string;
  type: QuestionType;
  options: string[];
}

export interface CreatePollData {
  title?: string;
  questions: CreateQuestionData[];
  expiryHours?: number;
}

// One answer in a batch submission.
export interface QuestionAnswer {
  questionId: string;
  optionIndex: number;
  textAnswer?: string;
}

export interface OptionResult {
  optionIndex: number;
  text: string;
  voteCount: number;
  percentage: number;
}

export interface TextAnswer {
  text: string;
  authorName: string | null; // null = Anonymous (guest)
  authorRole: string | null; // "User" | "Admin" | null
  votedAt: string; // ISO 8601
}

// Live results for one survey question.
export interface QuestionResults {
  questionId: string;
  questionIndex: number;
  text: string;
  type: string; // QuestionType
  totalVotes: number;
  options: OptionResult[];
  textAnswers: TextAnswer[];
}

export interface VoteResults {
  pollCode: string;
  title: string | null;
  isActive: boolean;
  totalVoters: number;
  questions: QuestionResults[];
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

export interface QuestionAnalytics {
  questionId: string;
  questionIndex: number;
  text: string;
  type: string; // QuestionType
  totalVotes: number;
  topOption: TopOption | null;
}

export interface Analytics {
  pollCode: string;
  title: string | null;
  totalVoters: number;
  timeline: TimeBucket[];
  peakMinute: TimeBucket | null;
  questions: QuestionAnalytics[];
}

// ── Audience Q&A / "Ask" (Vote API via Gateway + SignalR) ────
export interface AudienceQuestion {
  id: string;
  text: string;
  upvotes: number;
  isPinned: boolean;
  createdAt: string;
}
