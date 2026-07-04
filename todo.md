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

## Phase 12 — Role-Based Access Control (Guest / User / Admin)  `[RBAC]`

**Goal:** introduce three roles — **Guest** (no token), **User** (logged in), **Admin** — enforced at the
**Gateway** (coarse, route-level), **each service** (fine/ownership), and the **frontend** (UX guards;
defense-in-depth, server still authoritative).

**Decisions locked:**
- **Create poll** → login required (Guest ❌). · **Creator analytics** → owner + admin only (Guest ❌).
- **Q&A upvote** → **one per person per question** (Guest by browser token, User by account; **no login required**).
- **Ask Q&A** → stays open/anonymous (Guest ✅). · **Vote** → unchanged (Guest ✅, browser-token dedup).
- **Pin/hide Q&A** + **close/delete poll** → owner + admin. · **User mgmt + global dashboard** → admin only.

**Permission matrix**

| Capability | Guest | User | Admin |
|---|:--:|:--:|:--:|
| View poll / vote / live results | ✅ | ✅ | ✅ |
| Ask Q&A (anonymous) · Upvote (1×/person) | ✅ | ✅ | ✅ |
| Create a poll | ❌ | ✅ | ✅ |
| View creator analytics | ❌ | ✅ own | ✅ any |
| My Polls · close · delete · pin/hide | ❌ | ✅ own | ✅ any |
| Manage users · global dashboard | ❌ | ❌ | ✅ |

**Skills:** `pollbuilder-backend`, `pollbuilder-database`, `pollbuilder-frontend`, `pollbuilder-testing`, `test-driven-development`, `verification-before-completion`.

> **Enforcement model:** JWT gains a `role` claim → Gateway forwards `X-User-Role` (code transform, anti-spoof
> like `X-User-Id`) → services do owner/admin checks. "Own resource" = `X-User-Id == Poll.CreatorId`; admin = `role==Admin` bypass.

---

### Phase 12.1 — Identity: roles + admin seed  `[RBAC]`  ✅ DONE
**Goal:** issue role-aware JWTs and bootstrap the first admin.
- [x] Added `Role` to `User` (default `"User"`); `Role nvarchar(20) NOT NULL default 'User'` via `IdentityDbContext`; migration `AddUserRole` (backfills existing rows → `User`).
- [x] `AuthService.GenerateToken` adds a `role` claim; register defaults to `User` (model default).
- [x] Admin bootstrap on startup: promotes emails in `Admin:Emails` (env `Admin__Emails__0…`) to `Admin`, idempotent (promotes existing accounts; guarded by `!EF.IsDesignTime` on migrate).
- [x] Unit tests: `Register_TokenHasRoleUser_ByDefault`, `Login_TokenHasRoleAdmin_WhenUserIsAdmin` (+ existing).
- **Files:** `Models/User.cs`, `Data/IdentityDbContext.cs` (+`Migrations/20260604_AddUserRole`), `Services/AuthService.cs`, `Program.cs` (seed + `EF.IsDesignTime` guard), `appsettings.json` (`Admin:Emails: []`), `IdentityApi.Tests/Services/AuthServiceTests.cs`.
- **DoD:** ✅ `dotnet test` identity green — **15 passed** (13 → 15); a promoted admin's login JWT contains `role=Admin`.

### Phase 12.2 — Gateway: propagate role + policies + routes  `[RBAC]`  ✅ DONE
**Goal:** forward role; gate routes; require auth where decided.
- [x] Code transform: strips + sets `X-User-Role` from the `role` claim (alongside `X-User-Id`, same anti-spoof pattern).
- [x] Added `admin` policy (`RequireAuthenticatedUser().RequireClaim("role","Admin")`).
- [x] New route `polls-create` → `POST /api/polls` → `authenticated` (Order 10, before the Order-100 catch-all) so **guests can't create**.
- [x] `vote-analytics` route → now `authenticated`.
- [x] Admin routes: `admin-polls` (`/api/admin/polls/{**remainder}` → poll-api, `admin`, Order 11), `admin-users` (`/api/admin/users/{**remainder}` → identity-api, `admin`, Order 12).
- [x] Public unchanged: GET poll, vote, results, questions list/ask/upvote; Dev overrides only cluster addresses (routes inherited).
- **Files:** `services/gateway/Gateway/Program.cs` (transform + `admin` policy), `appsettings.json` (routes).
- **DoD:** ✅ gateway builds 0 warnings/0 errors. *(401/403 runtime behaviour exercised in 12.7 integration/E2E.)*

### Phase 12.3 — Poll API: create-auth, owner-or-admin, admin list  `[RBAC]`  ✅ DONE
- [x] `Create` requires `X-User-Id` (401 if missing) → `CreatorId` always set (removed anonymous create).
- [x] `Close`/`Delete` take `isAdmin` → succeed for creator **or** `X-User-Role == Admin` (controller reads role via `IsAdmin()`).
- [x] Added `CreatorId` to `PollResponse` (consumed by Vote API analytics + frontend ownership).
- [x] Admin: new `AdminPollsController` `GET /api/admin/polls` (re-checks `X-User-Role`, 403 otherwise) + `PollRepository.GetAllAsync` + `PollService.GetAllAsync`.
- [x] Tests: create-no-user→**401**; admin close non-owner→**200**; admin-list→**200** / no-admin→**403**; service admin-bypass (close/delete) + `GetAll`; updated the Phase 9 create tests to send `X-User-Id` (anonymous create now blocked).
- **Files:** `Controllers/PollsController.cs` + new `AdminPollsController.cs`, `Services/PollService.cs`, `Repositories/PollRepository.cs`, `DTOs/PollResponse.cs`, `PollApi.Tests/Services/PollServiceTests.cs` + `Integration/PollEndpointTests.cs`.
- **DoD:** ✅ `dotnet test` poll-api green — **33 passed** (26 → 33); ownership + admin bypass enforced.

### Phase 12.4 — Vote API: analytics gate, upvote dedup, Q&A moderation  `[RBAC]`  ✅ DONE
- [x] `PollClientService.PollInfo` gained `CreatorId` (read from Poll API's response, added in 12.3).
- [x] **Analytics** (`GetAnalyticsAsync(code, userId, isAdmin)`) → allow if `userId == CreatorId` or admin, else `Forbidden` → controller maps to **403** (404 when poll missing).
- [x] **Upvote dedup (Option A):** new `QuestionUpvote { Id, QuestionId, VoterKey }`, unique `(QuestionId, VoterKey)`; migration `AddQuestionUpvotes`. `VoterKey = X-User-Id ?? body.voterToken`. Repeat → **409**; `Upvotes` incremented once per distinct voter.
- [x] **Pin** restricted to owner + admin (fetches `CreatorId`); added **`DELETE /api/polls/{code}/questions/{id}`** (owner/admin). Ask/list stay open.
- [x] Tests: upvote 200 / dup 409; analytics owner 200 / non-owner 403 / admin 404-when-missing; pin owner 200 / non-owner 403; service-level owner/admin/forbidden + dedup.
- **Files:** `Services/PollClientService.cs` (+`CreatorId`), new `Models/QuestionUpvote.cs`, `Data/VoteDbContext.cs` (+config, +`Migrations/20260604_AddQuestionUpvotes`), `Repositories/QuestionRepository.cs` (dedup + delete), `Services/QuestionService.cs` + `VoteService.cs`, `Controllers/QuestionsController.cs` + `VotesController.cs`, `DTOs/QuestionDtos.cs` (+`UpvoteRequest`), `Program.cs` (`EF.IsDesignTime` guard), `VoteApi.Tests/*` (+ `FakePollClientService.OwnerId`).
- **DoD:** ✅ `dotnet test` vote-api green — **43 passed** (35 → 43); dedup + analytics gate + pin ownership enforced.

### Phase 12.5 — Frontend: role-aware session + guards  `[RBAC]`  ✅ DONE
- [x] `auth/session`: decode JWT payload (base64url, no dep) → `getUserId()`, `getRole()`, `isAdmin()`.
- [x] Role read via session helpers in guards/pages (no change to the action-only `useAuth`); new shared `auth/voter.ts` (`getVoterToken`) reused by `useVote` + `useQuestions`.
- [x] `RequireAuth` + `RequireAdmin` route wrappers (redirect guests → `/login`, non-admins → `/`); `RequireAuth` wraps `/my-polls` (RequireAdmin used by `/admin` in 12.6).
- [x] Create guarded: guests get a **"log in to create"** CTA card (in-page; `/` stays reachable).
- [x] `PollInfo` type +`creatorId`; `canModerate = isAdmin() || creatorId===getUserId()` → **Pin** (QandAPanel) + **analytics link** (ResultsPage) only for owner/admin; ResultsPage fetches poll for ownership.
- [x] Upvote sends `voterToken` and **swallows 409** quietly.
- **Files:** `src/auth/session.ts`, new `src/auth/voter.ts`, new `src/components/RequireAuth.tsx` + `RequireAdmin.tsx`, `src/App.tsx`, `src/pages/CreatePollPage.tsx` + `ResultsPage.tsx` + `VotePage.tsx`, `src/components/QandAPanel.tsx`, `src/hooks/useQuestions.ts` + `useVote.ts`, `src/types/poll.types.ts`.
- **DoD:** ✅ `npm run lint` clean + `npm run build` green (620ms); guests see no Create form / analytics link / pin; owners/admins do; upvote dedups.

### Phase 12.6 — Admin dashboard (frontend) + identity admin-users API  `[RBAC]`  ✅ DONE
- [x] **Backend (identity):** new `AdminService` (list / setRole / deleteUser; blocks self role-change & self-delete) + `AdminUsersController` (`GET /api/admin/users`, `POST .../{id}/role`, `DELETE .../{id}`; re-checks `X-User-Role`) + `AdminUserResponse`/`SetRoleRequest` DTOs + DI. *(The gateway already routed `/api/admin/users` in 12.2; this fills in the missing controller.)*
- [x] `AdminDashboardPage` (`/admin`, wrapped in `RequireAdmin`): **all polls** (close/delete any) + **users** (list, promote/demote, delete) on glass cards.
- [x] `useAdmin` hook → loads `/admin/polls` + `/admin/users` in parallel; actions (closePoll/deletePoll/setRole/deleteUser) re-fetch on success.
- [x] Nav shows **Admin** link (`ShieldCheck`) only when `isAdmin()`.
- **Files:** identity `Services/AdminService.cs` + `Controllers/AdminUsersController.cs` + `DTOs/AuthDtos.cs` + `Program.cs` + `IdentityApi.Tests/Integration/AdminUsersEndpointTests.cs`; frontend new `src/pages/AdminDashboardPage.tsx` + `src/hooks/useAdmin.ts`, `src/App.tsx` (route + nav), `src/types/poll.types.ts` (+`AdminUser`), `src/index.css` (admin styles).
- **DoD:** ✅ identity `dotnet test` **18 passed** (15 → 18; +3 admin-users); frontend `npm run lint` clean + `npm run build` green (353ms; fixed one `set-state-in-effect`); admin manages polls + users from the UI; non-admins redirected from `/admin`.

### Phase 12.7 — Tests, docs, verify & deploy  `[RBAC]`  ✅ DONE (commit/push pending user OK)
- [x] Integration tests for 401/403/409 paths (header-driven, gateway-independent) — landed across 12.3/12.4/12.6: poll create-**401** + admin-polls **403**/200; vote upvote-**409** + analytics **403** + pin **403**; identity admin-users **403**.
- [x] Full backend sweep + frontend: **poll 33 · vote 43 · identity 18 = 94** green; frontend lint clean + build (406ms); gateway build 0 warnings.
- [x] Synced [ARCHITECTURE.md](ARCHITECTURE.md): stack auth/role rows; Gateway/Vote/Identity responsibilities; `User.Role` + `QuestionUpvote` entities + DB diagram + indexes; endpoint auth columns (create-auth, analytics-auth, pin/delete owner-admin, `/api/admin/*`); gateway transform (`X-User-Role`) + routing rows (10/11/12) + defense-in-depth note; auth data-flow (role claim); **new RBAC section + permission matrix**; env `Admin__Emails`; frontend routes (`/admin`); 3 design-decision rows; project-structure additions.
- [x] Updated [DEPLOYMENT.md](DEPLOYMENT.md): `Admin__Emails__0` on identity-api; RBAC migrations auto-apply note + first-admin promotion step.
- [ ] Commit + push (deploys via CI/CD) — **awaiting user go-ahead** (push triggers a production deploy; commit omits the Co-Authored-By trailer per your preference).
- **DoD:** ✅ all tests green; ARCHITECTURE/DEPLOYMENT synced. Deploy on your push.

**Definition of Done (Phase 12):** the matrix is enforced end-to-end — guests vote/ask/upvote/view but cannot create or see analytics; users own their polls; admins manage everything; one-upvote-per-person; lint+build+tests green; docs synced.

### Impact summary — files & schema this phase changes
- **New files:** `Models/QuestionUpvote.cs` (vote-api), `Migrations/AddUserRole` (identity), `Migrations/AddQuestionUpvotes` (vote), `RequireAuth.tsx` + `RequireAdmin.tsx` + `AdminDashboardPage.tsx` + `useAdmin.ts` (frontend).
- **Schema changes:** `IdentityDb.Users` +`Role`; `VoteDb` + `QuestionUpvotes` table (unique `(QuestionId, VoterKey)`) → **3 features need EF migrations** (auto-apply on startup).
- **Contract change:** `PollResponse`/`PollInfo` (+`frontend PollInfo`) gain `CreatorId`.
- **Behaviour changes (watch for regressions):** `POST /api/polls` now **401 without a token** (anonymous create removed); `GET …/analytics` now **auth+owner/admin**; `…/questions/{id}/pin` now **owner/admin only**; `…/upvote` now **deduplicated (409 on repeat)**. Update any tests/docs that assumed the old open behaviour (esp. Phase 9 integration tests + ARCHITECTURE.md endpoint/auth columns).

---

## Phase 13 — OpenText answers as social comments  `[UX]`  ✅ DONE

**Goal:** render each OpenText poll answer on the Results page as a social-media-style **comment**
(avatar + author name + role chip + the text). A **guest's** answer shows as **"Anonymous"**;
a **logged-in** user's answer shows their **name + role**.

**Decisions locked:**
- **Surface:** OpenText poll answers only — the anonymous Q&A panel is unchanged (stays anonymous by design).
- **Name source:** there is no name field in the system (`User` has only `Email`/`Role`; JWT carries
  `sub`/`email`/`role`). Use the **email local-part** (text before `@`).
- **Trust model:** **client-supplied label** — the SPA already decodes the JWT for UX only ("never for
  security") and sends name+role in the vote body; the Vote API stores them as-is. Spoofable, but a
  text-answer feed is not a security boundary, so **no Gateway change** is needed.

- [x] **Vote API schema:** `Vote` +`AuthorName`(64, nullable) +`AuthorRole`(20, nullable) in
  `Models/Vote.cs` + `Data/VoteDbContext.cs`; migration **`AddVoteAuthor`** (auto-applies on startup).
- [x] **Vote API contract:** `VoteRequest` +`AuthorName?`/`AuthorRole?`; `VoteResultsResponse.TextAnswers`
  changes `List<string>` → `List<TextAnswerResponse>` (`Text`, `AuthorName?`, `AuthorRole?`, `VotedAt`);
  `VoteRepository.GetTextAnswersAsync` projects the new shape; `VoteService` captures author in the
  OpenText branch of `SubmitVoteAsync`.
- [x] **Frontend:** `session.ts` +`getDisplayName()` (email local-part); `useVote` sends
  `authorName`/`authorRole`; `poll.types.ts` `textAnswers: TextAnswer[]`; `ResultsPage` renders comment
  cards (avatar / name-or-Anonymous / role chip / relative time / text); `index.css` `.comment*` styles.
- [x] **Tests:** update the OpenText results test to the new shape; add author-persisted + guest-null tests.
- [x] **Docs:** sync ARCHITECTURE.md (Vote entity + DB diagram + one design-decision row). No route/RBAC change.

**Definition of Done:** guest answers show "Anonymous", logged-in answers show name + role chip; the feed
updates live (SignalR already broadcasts results); `dotnet test` vote-api green; frontend lint+build clean;
ARCHITECTURE.md synced.

---

## Product polish — Phases 14–17  `[UX]`

Optional, **frontend-only** features for bonus credit + a stronger live demo. The app already clears the
Distinction band (all 4 brief additions + RBAC + comment feed), so these are extras — kept deliberately
simple. **No backend, schema, endpoint, or Gateway changes** in any of these phases. Build them
**one phase at a time on request** (log-first, like the rest of this plan).

**Shared scope decisions:**
- **Dark mode → app pages only**; the marketing landing (`/`) stays its current light design
  (`index.css` has ~56 hardcoded colors concentrated in the landing styles; app pages mostly use tokens).
- **CSV** is generated **client-side** from the already-loaded `VoteResults` — no new route.
- **Toasts** = a tiny **no-dependency** context (project's no-extra-lib style); **QR** adds one small
  library (`qrcode.react`, SVG output — crisp on a projector, offline, no external call).

---

### Phase 14 — QR code for live voting  `[UX]`  ✅ DONE
**Goal:** show a scannable QR of the vote link so an audience can scan → vote → watch the bars move live
(amplifies the existing SignalR feature — the best demo moment).
- [x] Add dependency `qrcode.react` (frontend). — `qrcode.react@4.2.0` (`QRCodeSVG`, SVG/offline).
- [x] Enhance [ShareLink.tsx](frontend/src/components/ShareLink.tsx) (already shown on Vote + Results
  pages): a **"Show QR"** toggle revealing `<QRCodeSVG value={url} size={180} />` (reuses the existing
  `url = origin + /poll/{code}`), collapsed by default. — wrapped in `.share-link-wrap`; toggle uses the `QrCode` icon.
- [x] `.share-link__qr` styles in [index.css](frontend/src/index.css) (token-based; white quiet-zone padding).
- [x] Sync [ARCHITECTURE.md](ARCHITECTURE.md): structure-tree note on ShareLink QR + one frontend design-decision line.
- **DoD:** ✅ `npm run lint` clean + `npm run build` green; QR encodes the `/poll/{code}` URL; scanning opens the vote page and a vote updates the live chart (SignalR).

### Phase 15 — Export results to CSV  `[UX]`  ✅ DONE
**Goal:** a "Download CSV" of a poll's results, generated client-side (no endpoint).
- [x] New util `frontend/src/utils/csv.ts` — `downloadCsv(filename, headers, rows)` (RFC-escapes fields
  with `,"`/newlines; UTF-8 BOM for Excel; `Blob` + temporary `<a download>`; revokes the object URL).
- [x] "Download CSV" button on [ResultsPage.tsx](frontend/src/pages/ResultsPage.tsx) (`results-foot`):
  choice/rating/yes-no → `Option, Votes, Percentage` (+ a total row); OpenText → `Author, Role, Answer,
  SubmittedAt` (author `?? "Anonymous"`). Filename `poll-{code}-results.csv`. Uses the `Download` icon; grouped in `.results-actions`.
- [x] Sync [ARCHITECTURE.md](ARCHITECTURE.md): structure-tree `utils/csv.ts` + a design-decision line (client-side CSV).
- **DoD:** ✅ lint + build clean; button builds a correct CSV for both choice polls and OpenText polls from in-memory results.

### Phase 16 — Toast notifications  `[UX]`  ✅ DONE
**Goal:** lightweight action feedback, with no dependency.
- [x] New `frontend/src/components/Toast.tsx`: `ToastProvider` (context), `useToast()` →
  `toast(message, kind?: 'success' | 'error')`, and a viewport (fixed corner, auto-dismiss ~3s, dismiss on click).
- [x] Mount in [App.tsx](frontend/src/App.tsx): wrap `Layout` in `<ToastProvider>` (inside `BrowserRouter`).
- [x] Wire `toast(...)` at existing success/error points: ShareLink copy, [CreatePollPage.tsx](frontend/src/pages/CreatePollPage.tsx)
  created, close/delete in `MyPollsPage` + [useAdmin.ts](frontend/src/hooks/useAdmin.ts)/`AdminDashboardPage` (success + error toasts).
- [x] `.toast*` styles in [index.css](frontend/src/index.css) (token-based → themes automatically).
- [x] Sync [ARCHITECTURE.md](ARCHITECTURE.md): structure-tree `components/Toast.tsx` + a design-decision line.
- **DoD:** ✅ create / copy / close / delete raise toasts; lint + build clean.

### Phase 17 — Dark mode (app pages)  `[UX]`  ✅ DONE
**Goal:** a light/dark toggle for the app pages; the marketing landing stays light by design.
- [x] Dark token set in [index.css](frontend/src/index.css): `:root[data-theme="dark"] { --bg / --surface /
  --surface-solid / --surface-bd / --border / --ink / --ink-soft / --muted / --shadow }` (brand `--rose`/`--blue`/`--violet` kept).
- [x] Re-declared (in the dark block only) the app-page surfaces that hardcode a light background in light mode
  (`.btn-outline`/`.input`/`.seg-btn`/`.opt-remove`/`.vopt`/`.yesno-block`/`.rating-chip`/`.qanda-item`/`.qanda__upvote`/`.stat-card` → `--surface`;
  selected tints → `color-mix` rose; `.app-header`, `.brand__mark` fixed) — **light-mode CSS untouched**. Landing kept light via `main.lp` token re-assert.
- [x] Theme toggle in `Nav` ([App.tsx](frontend/src/App.tsx)) (sun/moon icon) via `useTheme` hook: sets
  `document.documentElement.dataset.theme`, persists `localStorage('theme')`, inits from stored value or `prefers-color-scheme`.
- [x] Sync [ARCHITECTURE.md](ARCHITECTURE.md): `useTheme.ts` in tree + a design-decision line (`data-theme` token overrides; app pages; landing light).
- **DoD:** ✅ toggle flips app pages light↔dark and persists across reload; landing stays light; lint + build clean.

---

## Phase 18 — UI redesign: Tailwind CSS + "Election Night" identity  `[UX]`

**Goal:** move the frontend off the hand-rolled `index.css` design system onto **Tailwind CSS v4**, and
replace the Mentimeter-clone look (navy + pink, Inter) with a distinct identity. Driven by the
`impeccable` skill. **Frontend-only; no backend/schema/API change.** Strategic + visual context in
[PRODUCT.md](PRODUCT.md) + [DESIGN.md](DESIGN.md).

**Identity history:** a first attempt ("Rally" — warm/light, full palette) was **rejected** for reading
as a generic recolored SaaS template (same hero+card-grid+steps+CTA skeleton). Replaced with:

**"Election Night" identity (locked):** the app looks like a **live broadcast results board** —
- **Palette (drenched dark):** bg `#0B0913`, panel `#15121F`, fg `#F5F2FB`, accents tangerine `#FF6B3D`
  / grape `#8B6BFF` / teal `#2DD4C4` (they **glow**) + amber `#FFC44D`. The glowing result bar is the
  structural motif everywhere; mono % + climbing counts carry "live".
- **Type:** Bricolage Grotesque (display) + Hanken Grotesk (body) + **Geist Mono** (all data) — none on
  impeccable's overused list.
- **Tooling:** Tailwind v4 via `@tailwindcss/vite`, CSS-first `@theme` tokens (no `tailwind.config.js`).
- **Coexistence (fixed):** Preflight skipped **and** the legacy `index.css` imported into the **lowest
  cascade layer** (`@layer legacy, …` + `@import './index.css' layer(legacy)`), so Tailwind utilities win
  on the landing while app pages keep their legacy styling. (Unlayered legacy CSS beating utilities was
  the bug that made the first dark build render old element styles under new backgrounds.)

**Sequencing (chosen): landing first → coexist → migrate the rest page-by-page. App becomes dark-first.**

### Phase 18.1 — Tailwind setup + Election Night landing  `[UX]`  ✅ DONE
- [x] Installed `tailwindcss@4.3.1` + `@tailwindcss/vite`; fonts `@fontsource/bricolage-grotesque`,
  `@fontsource-variable/hanken-grotesk`, `@fontsource/geist-mono`
- [x] `vite.config.ts`: added the Tailwind plugin
- [x] `src/tailwind.css`: cascade-layer order (`legacy < theme/base/components/utilities`) + `@import
  './index.css' layer(legacy)`; `@theme` Election Night tokens (dark + glow); `.board-*` motion (scoped)
- [x] Rebuilt `pages/HomePage.tsx` as a broadcast results board — interactive votable hero board, mono
  ticker, 4 question types as mini results boards, control-room capability list, big mono `01/02/03`
  rundown, glowing CTA band. On-mount entrances (no scroll-gated visibility)
- [x] Election Night landing chrome in `App.tsx` (`BoardNav` + `BoardFooter`) via `Layout` `isLanding`;
  removed orphaned `RallyNav`/`RallyFooter`/`LandingFooter`; **app `Nav`/`Footer` untouched**
- [x] Updated [PRODUCT.md](PRODUCT.md) + [DESIGN.md](DESIGN.md)
- [x] Verified: `npm run lint` clean + `npm run build` green; Playwright screenshots (desktop/mobile/voted)
  confirm dark board reads as a broadcast, glowing bars, mono numbers, contrast holds, no mobile overflow.
  Fixed the cascade-layer bug (legacy CSS was overriding utilities → old element styles bled through)
- **DoD:** ✅ landing renders in the Election Night identity; app pages still run on the legacy CSS; lint + build green.
- **Deferred to 18.2 (legacy `index.css`, app-page scope):** impeccable flagged 2 pre-existing bans there
  — gradient text `.h-gradient` (L131) and the toast side-stripe `border-left` (L2015). Fix during migration.
- **Cleanup:** remove the temp Playwright screenshot scripts/PNGs (`frontend/shot.mjs`, `check.mjs`, `shot-*.png`).

### Phase 18.2 — App pages → Election Night (token re-palette, hybrid)  `[UX]`  ✅ DONE
Chosen approach: **re-palette the token-driven legacy `index.css`** (fast, low-risk, whole app at once)
rather than a page-by-page Tailwind rewrite — the app already had a complete dark-mode token system (Phase 17).
- [x] Base `:root` accents → Election Night (`--rose` tangerine, `--blue` teal, `--violet` grape,
  brightened `--success`/`--danger`, tangerine `--glow`); fonts → Bricolage / Hanken / Geist Mono; `color-scheme: dark`
- [x] `:root[data-theme='dark']` neutrals → studio palette (`--bg #0B0913`, `--surface #15121F`, `--ink #F5F2FB`,
  `--muted #A79FB8`, hairline borders); `.app-header` tint → plum; removed obsolete light-landing re-asserts
- [x] **Dark-first:** `<html data-theme="dark">`; removed the theme toggle + `useTheme` usage from `App.tsx`; deleted `hooks/useTheme.ts`
- [x] `LiveBarChart` bar fills gained accent glows; leftover hardcoded light chips fixed (active nav pill, pin-flag, lead-tag, closed-notice)
- [x] **impeccable bans cleared in `index.css`:** gradient text (`.h-gradient` → solid tangerine) + toast side-stripe (`border-left` → full border + icon color)
- [x] Verified: `npm run lint` clean + `npm run build` green; Playwright screenshots (login/create/register) confirm cohesive dark Election Night across the app shell + forms
- [x] Synced [ARCHITECTURE.md](ARCHITECTURE.md) + [DESIGN.md](DESIGN.md)
- **DoD:** ✅ every app page renders in the unified dark Election Night identity; app is dark-only; lint + build green.

### Phase 18.3 — Tailwind rebuild of the core app screens  `[UX]`  🟡 IN PROGRESS
**Done (this pass):** rebuilt the six core screens + their shared components as native Tailwind
utilities on the Election Night system (via `shape`). Expression split per the brief — **restrained**
forms (Login, Register, Create), **broadcast** data screens (Vote, Results, Admin).
- [x] Added app-screen component vocabulary to [tailwind.css](frontend/src/tailwind.css) (`@layer components`): `.board-panel`, `.board-label`, `.board-input`, `.board-btn`(+`--block`), `.board-btn-outline`, `.board-bar-track`/`.board-bar-fill(--lead|teal|grape)`, `.board-spin`; added `--color-danger` token.
- [x] Login + Register — restrained centered dark auth forms.
- [x] Create poll + `PollForm` — restrained; auth-gate + success states.
- [x] `ShareLink` — dark row; QR kept on a white quiet-zone for scannability.
- [x] Vote + `VoteForm` — broadcast question + per-type controls (choice rows, Yes/No, 1–5, open-text).
- [x] `QandAPanel` — dark Q&A list/upvote/pin.
- [x] Results + `LiveBarChart` — glowing bars, LIVE pulse, mono figures, comment feed.
- [x] Admin — control-room rows (not cards), status/role pills, danger actions.
- [x] Verified: `npm run lint` clean + `npm run build` green; new classes/tokens present in compiled CSS. **Runtime-verified against a full local stack** (LocalDB + all 4 services via `dotnet run`, seeded polls of every type + votes): Playwright screenshots desktop + mobile of Login/Register/Create-gate, Vote (choice/yes-no/rating/open-text), Results (glowing bars, rating histogram, comment feed, LIVE pulse, mono figures), and Admin (dense rows, status/role pills) — all on-brand, correct contrast, no mobile overflow.
- [ ] **Remaining (later pass):** convert My Polls + Analytics, then retire `index.css` + the `legacy` cascade layer.
- **DoD (full):** every app file on Tailwind utilities; `index.css` removed; lint + build + backend tests still green. *(Not required for the demo — the app is already unified and dark-only.)*

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
        → 12 (RBAC: Guest/User/Admin)
```

**Phase 12 internal order:** 12.1 (identity roles) → 12.2 (gateway policies) → 12.3 (poll-api) →
12.4 (vote-api) → 12.5 (frontend guards) → 12.6 (admin dashboard) → 12.7 (tests/docs/deploy).

**Minimum for a Pass:** Phases 0–8 + 11. **Merit:** add Phases 9–10 (≥2 merit items). **Distinction:** add a Phase 10 distinction feature + thorough docs and clear design rationale.
