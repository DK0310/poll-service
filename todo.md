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

- [x] Scaffold `services/vote-api/` (`VoteApi.sln` classic format + `VoteApi.Tests`, net10.0, EF Core 10.0.8)
- [x] `Vote` model + `VoteDbContext` (unique `(PollCode, VoterToken)`, `(PollCode, OptionIndex)`, `VotedAt` indexes) + `InitialCreate` migration — schema verified vs ARCHITECTURE.md
- [x] `VoteRepository`: add, `HasVotedAsync`, `GetVoteCountsAsync` (SQL `GROUP BY`) — methods `virtual`
- [x] `PollClientService` — typed `HttpClient` (`AddHttpClient<T>`), base URL from `Services:PollApi`; returns `null` on non-2xx / `HttpRequestException`; co-located `PollInfo`/`PollOptionInfo`
- [x] `VoteService` (`Result<T>`): poll exists+active → option index → voter token → duplicate → save → build results (SignalR broadcast deferred to Phase 5)
- [x] DTOs: `VoteRequest`, `VoteResultsResponse` (+ `OptionResult` with percentages); plus `Common/Result<T>`
- [x] `VotesController`: `POST /api/polls/{code}/vote` (200 / 409 duplicate / 404 not-found / 400), `GET /api/polls/{code}/results` (200 / 404)
- [x] **Unit tests:** success, persisted-vote shape, poll-not-found, poll-closed, invalid/negative option, empty token, duplicate, results aggregation + zero-votes + not-found — **11/11 passing**

**Definition of Done:** `dotnet test services/vote-api/VoteApi.sln` green ✅ (11/11, 0 warnings). Live vote-against-running-Poll-API exercised end-to-end in Phase 3+ (Gateway) / Phase 7 (docker SQL Server); logic fully covered by mocked-collaborator unit tests.

---

## Phase 3 — API Gateway (YARP routing)  `[PASS]`

**Goal:** single front door so the frontend only ever talks to one URL. (JWT validation added in Phase 6.)
**Skills:** `pollbuilder-backend` (Gateway section), `pollbuilder-architecture`.

- [x] Scaffold `services/gateway/` (`Gateway.sln` classic format, net10.0) with YARP 2.3.0 (`AddReverseProxy().LoadFromConfig`)
- [x] Route + cluster config per [ARCHITECTURE.md → Gateway Routing Table](ARCHITECTURE.md) — live subset: `vote-submit`(1), `vote-results`(2), `signalr-hub`(3), `polls-public`(100) catch-all; clusters `poll-api`/`vote-api`. **Auth routes (`auth-route`, `polls-protected/close/delete`) + `authenticated` policy + `X-User-Id` transforms + `identity-api` cluster + JWT deferred to Phase 6**
- [x] CORS policy `Frontend` (origin from `Frontend:Url`, `AllowCredentials` for Phase 5 SignalR)
- [x] `appsettings.Development.json` overrides cluster addresses → `localhost:5001/5002` for local (non-docker) runs; base `appsettings.json` keeps docker service-name addresses per ARCHITECTURE
- [x] **Runtime routing verified** through `:5000` (poll-api+vote-api+gateway running): `/api/polls/{code}`→poll-api (500 no-DB JSON), `/api/polls/{code}/results` & `/vote`→vote-api (404 "Poll not found"), `/nope`→gateway no-route 404. Distinct response signatures prove correct clusters + ordering (no shadowing). Bonus: real vote→poll inter-service chain confirmed end-to-end.

**Definition of Done:** all current endpoints reachable through the Gateway on port 5000 ✅; route ordering correct (no shadowing) ✅. Build 0 warnings.

---

## Phase 4 — Frontend Core (create → vote → results, polling)  `[PASS]`

**Goal:** the full user flow working end-to-end in the browser, results via HTTP polling (SignalR comes next).
**Skills:** `pollbuilder-frontend`, `webapp-testing`.

- [x] Scaffold `frontend/` (React **19** + TS + Vite **8**, react-router-dom 7, axios) — Vite template pulled React 19 (not 18); ARCHITECTURE.md stack updated
- [x] `api/api.ts` Axios instance → Gateway (`VITE_API_URL`, localhost fallback) + JWT request interceptor + `apiErrorMessage` helper (401→/login response interceptor deferred to Phase 6)
- [x] `types/poll.types.ts` matching the API contract (PollInfo, PollOption, CreatePollData, VoteResults, OptionResult)
- [x] Hooks: `useCreatePoll`, `usePollInfo`, `useVote` (persistent `voterToken` in localStorage), `usePolledResults` (temp interval polling — clearly marked for Phase 5 swap)
- [x] Components: `PollForm` (2–6 options, expiry select), `VoteForm`, `LiveBarChart` (animated bars), `ShareLink`
- [x] Pages + routes: `CreatePollPage` (`/`), `VotePage` (`/poll/:code`), `ResultsPage` (`/poll/:code/results`) + `App.tsx` router (login/register/my-polls deferred to Phase 6); themed `index.css`
- [x] Secrets-conscious env: committed `frontend/.env.example`; local `frontend/.env` gitignored (VITE_ vars are public, not secret)
- [x] **Verified:** `npm run build` (tsc typecheck + vite build) green; `npm run lint` (eslint) **0 issues**; Vite dev server serves the SPA (title + root)
- [x] **Real DB-backed E2E** (Docker daemon was down → used LocalDB `MSSQLLocalDB` via env-override): applied both migrations to real DBs, ran poll-api+vote-api+gateway, drove the full flow through `:5000` — CREATE 201 (persisted), GET 200, VOTE 200, duplicate **409**, second voter 200, RESULTS `totalVotes:2` 50/50/0. Exact contract the frontend consumes.

**Definition of Done:** create → vote → results works end-to-end against a real DB through the Gateway ✅ (verified via the live HTTP flow + frontend build/lint/serve). Full in-browser click-through (Playwright) and SignalR live updates come with Phase 5; polling hook is in place now.

---

## Phase 5 — Real-Time Results (SignalR)  `[PASS]`

**Goal:** replace polling with live WebSocket updates.
**Skills:** `pollbuilder-backend` (SignalR), `pollbuilder-frontend` (`useLiveResults`).

- [x] Vote API: `PollHub` (`JoinPollGroup`/`LeavePollGroup`), `AddSignalR()`, `MapHub("/hubs/poll")`
- [x] `VoteService` broadcasts `ReceiveVoteUpdate` to `Group(code)` via `IHubContext<PollHub>` after a successful vote (step 7, before returning)
- [x] Vote API CORS policy `SignalR` with `AllowCredentials()` (origin from `Gateway:Url`); `appsettings` `Gateway:Url` added
- [x] Gateway already proxies `/hubs/{**}` → vote-api (Phase 3); YARP handles the WebSocket upgrade automatically — confirmed working end-to-end
- [x] Frontend: `@microsoft/signalr` 10.0 installed; `useLiveResults` hook (initial REST snapshot + SignalR w/ `withAutomaticReconnect`, JoinPollGroup, cleanup→LeavePollGroup+stop); `ResultsPage` swapped off polling (● Live / ○ Connecting badge); `usePolledResults` removed
- [x] **Unit test:** mock `IHubContext` (Clients.Group→IClientProxy), verify `SendCoreAsync("ReceiveVoteUpdate", …)` **Times.Once** on success and **Times.Never** on duplicate — VoteApi **11/11 green**
- [x] **Real SignalR E2E** (SQLEXPRESS + 3 services + gateway): Node `@microsoft/signalr` client connected to the hub **through the Gateway**, joined the group, a vote (HTTP 200) pushed `ReceiveVoteUpdate` over WebSocket with correct results (`totalVotes:1`, Yes 100%). Temp script removed after.

**Definition of Done:** a vote pushes a live update to connected clients with no refresh ✅ (proven via a real WebSocket client through the Gateway). Frontend `useLiveResults` drives `ResultsPage`; backend broadcast unit-tested. Two-browser-window scenario = the same flow with a UI client.

---

## Phase 6 — Identity API + Authentication  `[PASS]`

**Goal:** registration/login + creator-only actions, with JWT validated centrally at the Gateway.
**Skills:** `pollbuilder-backend`, `pollbuilder-database`, `pollbuilder-frontend`.

- [x] Scaffold `services/identity-api/` (`IdentityApi.sln` + `IdentityApi.Tests`, net10.0; EF Core 10, BCrypt.Net-Next 4.2, System.IdentityModel.Tokens.Jwt 8.18)
- [x] `User` model + `IdentityDbContext` (unique `Email`) + `InitialCreate` migration — applied to SQLEXPRESS `IdentityDb`
- [x] `AuthService`: register (BCrypt hash, case-insensitive duplicate-email guard, min 6-char password), login (no account enumeration), JWT generation (7-day, `sub`/`email`/`jti` claims)
- [x] `AuthController`: `POST /api/auth/register`, `POST /api/auth/login` (failures 400, not 401, so the SPA's 401 handler doesn't hijack login)
- [x] Gateway: JWT validation (`MapInboundClaims=false`) + `authenticated` policy + `auth-route` + `identity-api` cluster + protected routes; **`X-User-Id` set by a YARP code transform from the `sub` claim** (config `{claim:..}` isn't supported) **+ strips client-supplied `X-User-Id`** (anti-spoof)
- [x] Poll API: `PATCH /close`, `DELETE`, `GET /my-polls` read `X-User-Id` (401 missing / 403 non-creator / 404 / 204); `CreatorId` set on create
- [x] Frontend: `LoginPage`, `RegisterPage`, `MyPollsPage` + `PollCard`, `useAuth`, `useMyPolls`, `auth/session` (token + `auth-change` event), global **401 response interceptor** → `/login`, nav login/logout, routes
- [x] **Unit tests:** AuthService register success/empty-email/weak-password/duplicate/case-insensitive; login valid/bad-password/no-user — **8/8**; poll close/delete creator-vs-non-creator already covered (Phase 1)
- [x] `Jwt:Secret` identical in Gateway + Identity API — stored in **User Secrets** for both (verified equal by hash), placeholders in committed `appsettings.json`

**Definition of Done:** ✅ Verified end-to-end on SQLEXPRESS through the Gateway — register→token, authenticated create attributed (shows in `/my-polls`), other user sees `[]`, no-token→401, non-creator close→403, creator close→200, delete→204, spoofed `X-User-Id` stripped. All builds 0 warnings; **41 unit tests** (14 poll + 11 vote + 8 identity + 8 new) green; frontend build+lint clean.

---

## Phase 7 — Containerization & Local Orchestration  `[PASS]` / multi-stage `[MERIT]`

**Goal:** one command brings the whole system up locally.
**Skills:** `pollbuilder-devops`.

- [x] Multi-stage `Dockerfile` per backend service (`sdk:10.0` → `aspnet:10.0`, `UseAppHost=false`) `[MERIT]` size reduction
- [x] Frontend `Dockerfile` (`node:22-alpine` build → `nginx:alpine`) with relative `VITE_*` build args + `nginx.conf` (SPA fallback, proxy `/api` & `/hubs` to gateway, WS upgrade headers)
- [x] `.dockerignore` per service (excludes bin/obj/tests) + frontend (excludes node_modules/dist/.env)
- [x] Root `docker-compose.yml`: `db` (healthcheck) + `poll/vote/identity` + `gateway` + `frontend`; **only 5000 + 5173 published**; `depends_on: db service_healthy`; secrets via `${SA_PASSWORD}`/`${JWT_SECRET}` from gitignored root `.env` (+ committed `.env.example`)
- [x] **Migrations auto-apply on startup** (`MigrateAsync` + 12× retry, guarded by `IsRelational()` so integration tests skip it; `EnableRetryOnFailure`) — replaces the manual `dotnet ef` step (runtime images have no SDK). ARCHITECTURE.md updated.
- [x] **Offline verification:** all backends build; **33 unit tests** green; `docker compose config` validates (YAML + `${VAR}` interpolation); poll-api boots against SQLEXPRESS and **runs the migrator on startup** ("database already up to date") then serves (create 201); frontend builds with relative env (`/hubs/poll` baked, localhost fallback folded out)
- [ ] ⏳ **Smoke test `docker-compose up --build`** — **blocked: Docker Desktop daemon is not running** (`docker info` fails). Everything else is in place; run `docker-compose up --build` once Docker is started, then open `http://localhost:5173`.

**Definition of Done:** fresh `docker-compose up --build` + auto-migrations yields a working app; only Gateway (5000) and Frontend (5173) exposed. ✅ All artifacts written & validated offline; ⏳ the live `up` smoke test awaits Docker Desktop being started.

---

## Phase 8 — CI/CD & Cloud Deployment  `[PASS]` / lint step `[MERIT]`

**Goal:** push to `main` → automatically build, push images, and deploy. **A non-deployed app cannot score above 4.**
**Skills:** `pollbuilder-devops`, `verification-before-completion`.

- [x] `.github/workflows/ci-cd.yml` Job 1 — lint & test: `dotnet test` per service (Release) + frontend `npm ci`/`npm run lint`/`npm run build` (`[MERIT]` lint/static-analysis step) — runs on push **and** PR
- [x] Job 2 — build & push 5 images to Docker Hub (only on `main`, `docker/build-push-action@v6`, per-image GHA layer cache scopes)
- [x] Job 3 — deploy via Render webhook per service (each step **env-guarded** → no-ops until its `RENDER_HOOK_*` secret is set, so the pipeline stays green during incremental setup)
- [x] **Validated offline:** YAML parses (js-yaml); every referenced `.sln`, `frontend/package-lock.json`, and the 5 build-context `Dockerfile`s exist; frontend `lint`/`build` scripts present. Removed the `.gitkeep` placeholder.
- [x] **[DEPLOYMENT.md](DEPLOYMENT.md)** written — GitHub secrets table, Docker Hub token, 5 Render Web Services + per-service prod env vars (connection strings, identical `Jwt__Secret`, gateway cluster overrides, frontend proxy), verify steps
- [x] **Configure GitHub secrets** (Docker Hub creds + Render hooks) — done
- [x] **Create Render services + SQL Server**; set production env vars — done
- [x] **Verify live push → green pipeline + reachable public URL** — done

**Definition of Done:** a commit to `main` deploys automatically; the live URL serves the app end-to-end. ✅ **Met** — pipeline authored & statically validated, deployment documented in [DEPLOYMENT.md](DEPLOYMENT.md), GitHub/Docker Hub/Render configured, and a push to `main` deploys to the live public URL.

---

## Phase 9 — Test Hardening (integration tests)  `[MERIT]`

**Goal:** raise confidence and meet the Merit integration-test bar.
**Skills:** `pollbuilder-testing`.

- [x] `CustomWebAppFactory` per service (swaps SQL Server → EF in-memory by removing all `DbContextOptions*` descriptors then re-`AddDbContext` InMemory; `UseEnvironment("Testing")`). Startup auto-migration is skipped automatically (InMemory → `IsRelational()` false). `public partial class Program` already exposed (Phase 1/2/6).
- [x] Poll API integration tests (6): create **201**, too-few-options **400**, get **200**/**404**, `my-polls` **401** + `close` **401** without `X-User-Id`
- [x] Vote API integration tests (5): vote **200** (tallies + percentage), duplicate **409**, missing-poll **404**, closed-poll **400**, results **200** — uses a `FakePollClientService` stub (no Poll API needed)
- [x] Identity API integration tests (5): register **200** (valid JWT shape), duplicate-email **400**, short-password **400**, login **200**, wrong-password **400** — factory injects a valid `Jwt:Secret` via config (Testing env skips User Secrets)
- [x] CI already runs `dotnet test` per service (Phase 8 workflow), which now executes unit **and** integration tests together; failure paths covered per the per-method checklist

**Definition of Done:** ✅ integration tests pass — **Poll 20** (14+6), **Vote 16** (11+5), **Identity 13** (8+5) = **49 tests total** (33 unit + 16 integration), all green, 0 warnings. CI runs them via the existing `dotnet test` steps.

---

## Phase 10 — Merit & Distinction Features  `[MERIT]` / `[DIST]`

**Goal:** earn the higher tiers. Implement **≥2 Merit** + **≥1 Distinction** (pick during Phase 0).
**Scope chosen: ALL FOUR features** (2 Merit + 2 Distinction).

### Merit options (chosen: both)
- [x] **Poll expiry auto-close** `[MERIT]` — `PollCleanupService` background hosted service (configurable `PollCleanup:IntervalSeconds`) closes expired polls via `CloseExpiredPollsAsync`; results page shows a "closed — final results" banner *(schema already supported `ExpiresAt`/`IsActive`)*
- [x] **Multiple question types** `[MERIT]` — SingleChoice / YesNo / 1–5 Rating / OpenText (stored in `Vote.TextAnswer`, not tallied); `PollQuestionType` enum (string-converted), `AddQuestionType` migration, `BuildOptionTexts`, DTO `Type` field, `VoteForm` renders by type, OpenText results list
- [x] *(Merit also satisfied by Phases 7 multi-stage, 8 lint, 9 integration tests)*

### Distinction options (chosen: both)
- [x] **Creator analytics dashboard** `[DIST]` — votes over time (`LineChart` SVG), peak voting minute, top option; `GET /api/polls/{code}/analytics` (gateway order 8), `GetAnalyticsAsync` (per-minute buckets), `useAnalytics` hook, `AnalyticsPage`
- [x] **Anonymous Q&A mode** `[DIST]` — `Question` entity + `AddQuestions` migration; `QuestionRepository`/`QuestionService`/`QuestionsController` (list/ask/upvote/pin); SignalR `ReceiveQuestionsUpdate`; gateway route order 9; `useQuestions` hook + `QandAPanel` on Vote & Results pages

**Definition of Done:** chosen features work end-to-end, are tested, and deploy cleanly; update [ARCHITECTURE.md](ARCHITECTURE.md) with any new endpoints/schema/flows. ✅ **DONE** — backend tests green (poll-api 26, vote-api 35, identity-api 13 = 74), gateway builds clean, frontend lint+build green, 3 migrations applied to SQLEXPRESS, ARCHITECTURE.md synced (entities/indexes/endpoints/gateway routes/structure/design decisions).

---

## Phase 11 — Documentation & Submission  `[PASS]` / thorough README `[DIST]`

**Goal:** make it presentable, reproducible, and submittable.

- [x] `README.md`: project description, features (incl. Merit/Dist), architecture summary + diagram, local setup (`docker-compose up`), live-URL slot, run-without-Docker, test commands, deployment pointer — rewritten with accurate stack (React 19, EF Core 10, .NET 10; auto-migrate, no manual `ef update`)
- [x] Re-sync [ARCHITECTURE.md](ARCHITECTURE.md) — done in Phase 10 + topology diagram (`VoteDb (Votes, Questions)`); matches what was built (entities, indexes, endpoints, gateway routes, structure, design decisions)
- [ ] Presentation slides: live demo, architecture overview, CI/CD walkthrough (live push → deploy), code walkthrough (SignalR is the most interesting part), hardest problem + solution *(team task — not auto-drafted)*
- [x] Individual-report template (500+ words each) drafted → [docs/INDIVIDUAL_REPORT_TEMPLATE.md](docs/INDIVIDUAL_REPORT_TEMPLATE.md); each member fills in their own
- [x] Final check: workflow file present (`.github/workflows/ci-cd.yml`), all docs cross-link; ⏳ paste live Render URL into README + confirm repo public before presentation

**Definition of Done:** another developer can clone, run, and understand the project from the README alone ✅; live deployment verified before presentation day (Phase 8 deploy done — paste the public URL into the README's live-demo slot).

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
