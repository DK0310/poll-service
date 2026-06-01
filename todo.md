# Poll & Survey Builder — Build Plan (todo.md)

Phased plan to take this repo from **docs-only** to a **deployed, tested, real-time** microservices app for AMD201.

**How to read this:** work top-to-bottom. Each phase has a goal, an ordered task list, and a **Definition of Done** that must be green before moving on. Tier tags show what each phase earns:
`[PASS]` core requirement · `[MERIT]` merit bonus · `[DIST]` distinction bonus.

**Source of truth:** [ARCHITECTURE.md](ARCHITECTURE.md) for all structure, schema, ports, routes, env vars, and flows. Skills referenced per phase live in `.claude/skills/pollbuilder-*`.

**Build philosophy (from the brief):**
- Get the **core poll → vote → results flow working end-to-end first**, with plain HTTP polling.
- **Layer SignalR in afterwards**, replacing the polling.
- **Do not start with auth** — add it once the core flow works.
- Write tests **alongside** each feature (TDD), not at the end.

**Recommended grade target features** (pick before Phase 10):
- Merit #1: Poll expiry auto-close + final-results banner *(already in the schema/design)*
- Merit #2: Analytics-ready data **or** multiple question types
- Distinction: Creator analytics dashboard (votes over time, peak minute, top option)

---

## Phase 0 — Repo Foundations & Tooling  `[PASS]`

**Goal:** a clean monorepo skeleton everyone can clone and build.

- [x] Add a root `.gitignore` (.NET `bin/`/`obj/`, `node_modules/`, `.env`, `*.user`, build output) — verified it catches `bin/`, `node_modules/`, `.env`
- [x] Confirm/extend the top-level folder layout from [ARCHITECTURE.md → Project Structure](ARCHITECTURE.md) (`services/`, `frontend/`, `.github/workflows/`) — created with `.gitkeep` placeholders
- [x] Decide solution-per-service vs. one solution — **decided: one `.sln` per service** (documented in [CONTRIBUTING.md](CONTRIBUTING.md))
- [x] Add a placeholder `README.md` (filled out in Phase 11)
- [x] Agree on branch strategy (feature branches → PR → `main`) and commit conventions — documented in [CONTRIBUTING.md](CONTRIBUTING.md)
- [x] Install local prerequisites — toolchain verified present (.NET SDK 10, Node 22, Docker 29 / Compose v5, dotnet-ef 10); required versions documented in [CONTRIBUTING.md](CONTRIBUTING.md)
- [x] **Target framework decided: `net10.0`** — ARCHITECTURE.md, README, CONTRIBUTING, Dockerfile templates, and test csproj refs all updated from .NET 8 → .NET 10
- [x] Added [.editorconfig](.editorconfig) for consistent C#/TS formatting (supports the "clean codebase" criterion)

**Definition of Done:** repo clones cleanly; `.gitignore` prevents build artifacts being committed; team aligned on workflow. ✅ **Met** (nothing committed yet — awaiting your go-ahead).

---

## Phase 1 — Poll API (core CRUD)  `[PASS]`

**Goal:** the foundational service — create and read polls — with tests, before anything depends on it.
**Skills:** `pollbuilder-backend`, `pollbuilder-database`, `pollbuilder-testing`, `test-driven-development`.

- [x] Scaffold `services/poll-api/` (ASP.NET Core **10** Web API + `PollApi.sln` + `PollApi.Tests`) — classic `.sln` (SDK defaulted to `.slnx`), EF Core 10.0.8 + Moq/Mvc.Testing/InMemory
- [x] Models: `Poll`, `PollOption`, `PollStatus` enum + computed `IsExpired`/`IsClosed`/`IsActive` (see [schema](ARCHITECTURE.md))
- [x] `PollDbContext` with fluent config (unique `Code`, enum→string, defaults, indexes, cascade delete)
- [x] EF migration `InitialCreate` for `PollDb` — verified schema matches ARCHITECTURE.md (unique Code, enum nvarchar(20), NEWID()/GETUTCDATE(), cascade, composite + CreatorId/ExpiresAt indexes)
- [x] `PollRepository` (get-by-code with ordered options, add, update, delete, get-by-creator, get-expired) — methods `virtual` for mockability
- [x] `PollService` returning `Result<T>` — validation (question required, 2–6 options, no empty options), unique 5-char code generation (+ Close/Delete/GetByCreator logic ready for Phase 6)
- [x] DTOs: `CreatePollRequest`, `PollResponse` (+ `OptionResponse`); plus `Common/Result<T>`
- [x] `PollsController`: `POST /api/polls`, `GET /api/polls/{code}` (close/delete/my-polls deferred to Phase 6)
- [x] `ErrorHandlingMiddleware` + `Program.cs` wiring (+ `public partial class Program` for Phase 9 integration tests)
- [x] **Unit tests:** create success + each validation failure; get found/not-found; +Close/Delete creator checks — **14/14 passing**

**Definition of Done:** `dotnet test services/poll-api/PollApi.sln` green ✅ (14/14, 0 warnings). Migration generated; applying it to a live DB happens in Phase 7 (docker SQL Server).

---

## Phase 2 — Vote API (voting + results, NO SignalR yet)  `[PASS]`

**Goal:** submit votes and read aggregated results, validating polls via an HTTP call to Poll API.
**Skills:** `pollbuilder-backend`, `pollbuilder-database`, `pollbuilder-testing`.

- [ ] Scaffold `services/vote-api/` (`VoteApi.sln` + `VoteApi.Tests`)
- [ ] `Vote` model + `VoteDbContext` (unique `(PollCode, VoterToken)`, aggregation index) + `InitialCreate` migration
- [ ] `VoteRepository`: add, `HasVotedAsync`, `GetVoteCountsAsync` (SQL `GROUP BY`, not in-memory)
- [ ] `PollClientService` — typed `HttpClient` (`AddHttpClient<T>`) to `http://poll-api:8080`; returns null/failure on non-2xx
- [ ] `VoteService` (`Result<T>`): validate poll exists+active → validate option index → check duplicate → save → build results
- [ ] DTOs: `VoteRequest`, `VoteResultsResponse` (+ `OptionResult` with percentages)
- [ ] `VotesController`: `POST /api/polls/{code}/vote` (409 on duplicate), `GET /api/polls/{code}/results`
- [ ] **Unit tests:** mock `PollClientService` + repo — success, poll-not-found, poll-closed, invalid option, duplicate vote, empty token

**Definition of Done:** `dotnet test services/vote-api/VoteApi.sln` green; with Poll API running, can vote and read results; duplicate vote rejected.

---

## Phase 3 — API Gateway (YARP routing)  `[PASS]`

**Goal:** single front door so the frontend only ever talks to one URL. (JWT validation added in Phase 6.)
**Skills:** `pollbuilder-backend` (Gateway section), `pollbuilder-architecture`.

- [ ] Scaffold `services/gateway/` (`Gateway.sln`) with YARP (`AddReverseProxy().LoadFromConfig`)
- [ ] Route + cluster config per [ARCHITECTURE.md → Gateway Routing Table](ARCHITECTURE.md) — specific routes (vote, results, hubs) before the catch-all poll route
- [ ] CORS policy for the frontend origin
- [ ] Verify routing: `GET /api/polls/{code}` and `POST /api/polls/{code}/vote` reach the right services through `:5000`

**Definition of Done:** all current endpoints reachable through the Gateway on port 5000; route ordering correct (no shadowing).

---

## Phase 4 — Frontend Core (create → vote → results, polling)  `[PASS]`

**Goal:** the full user flow working end-to-end in the browser, results via HTTP polling (SignalR comes next).
**Skills:** `pollbuilder-frontend`, `webapp-testing`.

- [ ] Scaffold `frontend/` (React 18 + TS + Vite), install deps, `react-router-dom`
- [ ] `api/api.ts` Axios instance → Gateway (`VITE_API_URL`) with request/response interceptors
- [ ] `types/poll.types.ts` matching the API contract
- [ ] Hooks: `useCreatePoll`, `usePollInfo`, `useVote` (with persistent `voterToken` in localStorage)
- [ ] Components: `PollForm` (2–6 options, expiry select), `VoteForm`, `LiveBarChart`, `ShareLink`
- [ ] Pages + routes: `CreatePollPage` (`/`), `VotePage` (`/poll/:code`), `ResultsPage` (`/poll/:code/results`)
- [ ] Temporary results polling on `ResultsPage` (interval refetch) — to be replaced in Phase 5
- [ ] Manual end-to-end check with Playwright/webapp-testing: create → share → vote → see results

**Definition of Done:** with Gateway + Poll + Vote APIs up, a user can create a poll, vote, and watch counts update (via polling) in the browser.

---

## Phase 5 — Real-Time Results (SignalR)  `[PASS]`

**Goal:** replace polling with live WebSocket updates.
**Skills:** `pollbuilder-backend` (SignalR), `pollbuilder-frontend` (`useLiveResults`).

- [ ] Vote API: `PollHub` (`JoinPollGroup`/`LeavePollGroup`), `AddSignalR()`, `MapHub("/hubs/poll")`
- [ ] `VoteService` broadcasts `ReceiveVoteUpdate` to `Group(code)` via `IHubContext` after a successful vote
- [ ] Vote API CORS with `AllowCredentials()` for the WebSocket
- [ ] Gateway: ensure `/hubs/{**}` route proxies the WebSocket (Upgrade/Connection headers)
- [ ] Frontend: `useLiveResults` hook (fetch initial snapshot + SignalR connection w/ `withAutomaticReconnect`, cleanup on unmount); swap polling out of `ResultsPage`
- [ ] **Unit test:** mock `IHubContext`, verify `SendAsync` is called on a successful vote

**Definition of Done:** two browser windows on the same results page — a vote in one updates the chart in the other with no refresh.

---

## Phase 6 — Identity API + Authentication  `[PASS]`

**Goal:** registration/login + creator-only actions, with JWT validated centrally at the Gateway.
**Skills:** `pollbuilder-backend`, `pollbuilder-database`, `pollbuilder-frontend`.

- [ ] Scaffold `services/identity-api/` (`IdentityApi.sln` + tests)
- [ ] `User` model + `IdentityDbContext` (unique `Email`) + `InitialCreate` migration
- [ ] `AuthService`: register (BCrypt hash, duplicate-email guard, min password length), login, JWT generation (7-day, `nameidentifier` claim)
- [ ] `AuthController`: `POST /api/auth/register`, `POST /api/auth/login`
- [ ] Gateway: add JWT validation + `authenticated` policy + `X-User-Id` transform on protected routes
- [ ] Poll API: finish `PATCH /close`, `DELETE`, `GET /my-polls` reading `X-User-Id`; set `CreatorId` on create
- [ ] Frontend: `LoginPage`, `RegisterPage`, `MyPollsPage`, `useMyPolls`, token storage + 401 handling
- [ ] **Unit tests:** register success/duplicate/weak-password; login success/bad-credentials; close/delete creator vs non-creator
- [ ] Verify: `Jwt__Secret` identical in Gateway + Identity API

**Definition of Done:** a user can register, log in, create a poll while authenticated, see it in "my polls," and close/delete it; non-creators get 403; protected routes return 401 without a token.

---

## Phase 7 — Containerization & Local Orchestration  `[PASS]` / multi-stage `[MERIT]`

**Goal:** one command brings the whole system up locally.
**Skills:** `pollbuilder-devops`.

- [ ] Multi-stage `Dockerfile` per backend service (`[MERIT]` for the size reduction)
- [ ] Frontend `Dockerfile` (Node build → Nginx) + `nginx.conf` (SPA fallback, proxy `/api` and `/hubs` to gateway, WS headers)
- [ ] `.dockerignore` per service/frontend
- [ ] Root `docker-compose.yml`: `db`, `gateway`, `poll-api`, `vote-api`, `identity-api`, `frontend` with env + `depends_on` (see [ARCHITECTURE.md → Deployment](ARCHITECTURE.md))
- [ ] Document migration step: `docker-compose exec <svc> dotnet ef database update`
- [ ] Smoke test: `docker-compose up --build` → full flow works via `localhost:5173`

**Definition of Done:** fresh `docker-compose up --build` + migrations yields a fully working app; only Gateway (5000) and Frontend (5173) exposed as entry points.

---

## Phase 8 — CI/CD & Cloud Deployment  `[PASS]` / lint step `[MERIT]`

**Goal:** push to `main` → automatically build, push images, and deploy. **A non-deployed app cannot score above 4.**
**Skills:** `pollbuilder-devops`, `verification-before-completion`.

- [ ] `.github/workflows/ci-cd.yml` Phase 1 — lint & test: `dotnet test` per service + `npm run lint` (`[MERIT]` lint/static-analysis step)
- [ ] Phase 2 — build & push 5 images to Docker Hub (only on `main`, with GHA layer cache)
- [ ] Phase 3 — deploy via Render webhook per service
- [ ] Configure GitHub secrets (Docker Hub creds + 5 Render hooks — see [ARCHITECTURE.md → Required GitHub Secrets](ARCHITECTURE.md))
- [ ] Create Render services + a managed/containerized SQL Server; set production env vars (real `Jwt__Secret`, connection strings, frontend `VITE_*` → deployed gateway)
- [ ] Verify a live push triggers a green pipeline and the **public URL is reachable**

**Definition of Done:** a commit to `main` deploys automatically; the live URL serves the working app end-to-end (create/vote/live-results) in the cloud.

---

## Phase 9 — Test Hardening (integration tests)  `[MERIT]`

**Goal:** raise confidence and meet the Merit integration-test bar.
**Skills:** `pollbuilder-testing`.

- [ ] `CustomWebAppFactory` per service (swap SQL Server for EF in-memory; expose `public partial class Program`)
- [ ] Poll API integration tests: create 201, validation 400, get 200/404
- [ ] Vote API integration tests: vote flow, duplicate 409, results
- [ ] Identity API integration tests: register/login happy + sad paths
- [ ] Confirm CI runs unit **and** integration tests; review failure-path coverage against the per-method checklist

**Definition of Done:** integration tests pass in CI; every service method has success + failure coverage.

---

## Phase 10 — Merit & Distinction Features  `[MERIT]` / `[DIST]`

**Goal:** earn the higher tiers. Implement **≥2 Merit** + **≥1 Distinction** (pick during Phase 0).

### Merit options (choose 2)
- [ ] **Poll expiry auto-close** `[MERIT]` — `PollCleanupService` background hosted service closes expired polls; results page shows a "closed — final results" banner *(schema already supports `ExpiresAt`/`IsActive`)*
- [ ] **Multiple question types** `[MERIT]` — yes/no, 1–5 rating, open-text (stored, not tallied); requires schema + DTO + UI extension
- [ ] *(Merit also satisfied by Phases 7 multi-stage, 8 lint, 9 integration tests)*

### Distinction options (choose 1)
- [ ] **Creator analytics dashboard** `[DIST]` — votes over time (line chart), peak voting minute, top-option trend (Vote API already indexes `VotedAt`); new endpoint + `useAnalytics` hook + page
- [ ] **Anonymous Q&A mode** `[DIST]` — respondents submit text questions with their vote; creator can upvote/pin live (new entity + SignalR events)

**Definition of Done:** chosen features work end-to-end, are tested, and deploy cleanly; update [ARCHITECTURE.md](ARCHITECTURE.md) with any new endpoints/schema/flows.

---

## Phase 11 — Documentation & Submission  `[PASS]` / thorough README `[DIST]`

**Goal:** make it presentable, reproducible, and submittable.

- [ ] `README.md`: project description, architecture summary + diagram link, local setup (`docker-compose up`), live URL, env-var list, test commands
- [ ] Re-sync [ARCHITECTURE.md](ARCHITECTURE.md) so it matches what was actually built (it is the source of truth)
- [ ] Presentation slides: live demo, architecture overview, CI/CD walkthrough (live push → deploy), code walkthrough (SignalR is the most interesting part), hardest problem + solution
- [ ] Confirm every team member can speak to their contribution; draft individual reports (500+ words each)
- [ ] Final check: live URL up, repo public/accessible, workflow files present

**Definition of Done:** another developer can clone, run, and understand the project from the README alone; live deployment verified before presentation day.

---

## Cross-cutting checklist (every phase)

- [ ] Tests written alongside the feature (`test-driven-development`)
- [ ] No service touches another service's database — cross-service data via HTTP only
- [ ] `Result<T>` for expected failures; controllers stay thin; DTOs (never entities) cross the API boundary
- [ ] Verify with real commands before claiming done (`verification-before-completion`)
- [ ] Keep [ARCHITECTURE.md](ARCHITECTURE.md) authoritative — update it when structure/schema/flows change

---

## Critical path (dependency order)

```
Phase 0 → 1 (Poll API) → 2 (Vote API) → 3 (Gateway) → 4 (Frontend core)
        → 5 (SignalR) → 6 (Auth) → 7 (Docker) → 8 (CI/CD + deploy)
        → 9 (integration tests) → 10 (Merit/Dist) → 11 (docs/submit)
```

**Minimum for a Pass:** Phases 0–8 + 11. **Merit:** add Phases 9–10 (≥2 merit items). **Distinction:** add a Phase 10 distinction feature + thorough docs and clear design rationale.
