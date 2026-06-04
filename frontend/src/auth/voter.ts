// One persistent voter token per browser — used to dedup votes and Q&A upvotes
// for guests (no login required). Logged-in actions dedup by user id server-side.
const VOTER_KEY = 'voter_token';

export function getVoterToken(): string {
  let token = localStorage.getItem(VOTER_KEY);
  if (!token) {
    token = crypto.randomUUID();
    localStorage.setItem(VOTER_KEY, token);
  }
  return token;
}
