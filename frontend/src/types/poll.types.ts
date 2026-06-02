// Shapes returned by the backend (camelCase JSON), reached via the Gateway.

export interface PollOption {
  optionIndex: number;
  text: string;
}

export interface PollInfo {
  code: string;
  question: string;
  status: string; // "Open" | "Closed"
  createdAt: string; // ISO 8601
  expiresAt: string | null;
  isActive: boolean;
  options: PollOption[];
  url: string; // "/poll/{code}"
}

export interface CreatePollData {
  question: string;
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
  totalVotes: number;
  isActive: boolean;
  options: OptionResult[];
}

// ── Auth (Identity API via Gateway) ─────────────────────────
export interface AuthResponse {
  token: string;
}
