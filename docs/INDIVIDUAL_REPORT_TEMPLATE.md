# Individual Report — AMD201 Poll & Survey Builder

> **One report per team member.** Copy this file to `docs/reports/<your-name>.md` and fill in every section
> in your own words. Target **500+ words** (the brief's minimum). Write in the first person about *your*
> contribution — markers compare reports, so do not copy each other. Replace every _italic prompt_ with prose
> and delete the prompts when done.
>
> **Evidence wins marks.** Wherever you make a claim ("I implemented the SignalR broadcast"), back it with a
> concrete pointer: a file path, a commit hash, a PR number, or a screenshot. Use the project's clickable link
> style, e.g. [VoteService.cs](../../services/vote-api/VoteApi/Services/VoteService.cs).

---

## 1. Name & role

- **Name:**
- **Primary area(s):** _e.g. Vote API + SignalR, Frontend, Gateway/Auth, DevOps/CI-CD, Poll API_

## 2. My contribution (what I built)

_List the specific features, services, components, or infrastructure you owned. Be concrete — name the files,
endpoints, hooks, or pipeline jobs. For each, say what it does and why it was needed._

- ...
- ...
- ...

**Evidence:** _link the key files/commits/PRs that show your work._

## 3. How it fits the architecture

_Explain how your part fits the microservices design in [ARCHITECTURE.md](../../ARCHITECTURE.md): which service
it lives in, what it owns, how it talks to other parts (HTTP via the Gateway, the SignalR hub, the database it
uses). Show you understand the "database-per-service" and "Gateway as single entry point" rules and that your
code respects them._

## 4. Key technical decisions I made

_Pick 2–3 decisions you were responsible for and justify them. Examples you might draw on: using `Result<T>`
instead of exceptions; validating the JWT only at the Gateway and forwarding `X-User-Id`; storing the question
type as a string enum; deduplicating votes by browser token; the background `PollCleanupService` for expiry.
For each: the options you considered, what you chose, and the trade-off._

## 5. Hardest problem & how I solved it

_Tell one debugging/design story end to end: the symptom, how you investigated, the root cause, and the fix.
Good candidates from this project: the YARP `{claim:...}` transform not being supported (so `X-User-Id` was
never set and the Poll API returned 401), shared in-memory test DB causing vote-tally tests to interfere, the
content-root/appsettings 404 issue when running compiled DLLs directly, or the nginx → HTTPS gateway SNI fix.
Use **your** problem if it differs._

## 6. Testing & verification

_What did you test and how? Reference your unit/integration tests and the commands you ran (e.g.
`dotnet test services/vote-api/VoteApi.sln` → 35 passing). Mention any real end-to-end checks (curl through the
Gateway, a SignalR client, the live deployment). Tie this to "evidence before assertions"._

## 7. What I learned

_Microservices concepts that clicked for you: service boundaries, inter-service HTTP, real-time with SignalR,
JWT at the gateway, containerization, CI/CD. Be specific about what was new._

## 8. What I'd improve with more time

_Honest limitations and next steps: e.g. message-queue events instead of synchronous HTTP, rate limiting,
richer analytics, accessibility, more integration coverage, observability/tracing._

---

### Checklist before submitting
- [ ] 500+ words, first person, in my own words (not copied from a teammate)
- [ ] Every claim backed by a file path / commit / screenshot
- [ ] I can verbally walk a marker through any file I referenced
- [ ] Spell-checked and proofread
