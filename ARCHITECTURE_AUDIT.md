# ARCHITECTURE.md ↔ Codebase Alignment Audit

**Date:** 2026-06-10 · **Doc audited:** [ARCHITECTURE.md](ARCHITECTURE.md) (HEAD `b948885`)
**Method:** every checkable claim in the doc was verified against the actual source — gateway config,
controller route attributes, models, DbContext index definitions, `Program.cs` files, docker-compose,
the CI workflow, and the frontend `src/` inventory + `App.tsx` routes.

**Verdict:** the backend sections are in excellent shape (gateway table, endpoints, schema, indexes,
RBAC, flows all match the code exactly). The **frontend sections are stale** — the Mentimeter
redesign's landing page + `/create` route move (commit `2ed2db1`) was never synced back into the doc.
One tech-stack claim and the production frontend-hosting description are also out of date.

---

## ❌ Mismatches (doc says one thing, code does another)

### 1. Frontend Routes table is wrong — `/` is no longer the Create form  `HIGH`
**Doc** (§ Frontend Routes, ~line 742): `/` → `CreatePollPage` "Poll creation form (guests see a
'log in to create' CTA)". No landing page, no `/create` row.
**Code** ([App.tsx:164-165](frontend/src/App.tsx#L164-L165)):
```tsx
<Route path="/" element={<HomePage />} />          // marketing landing page
<Route path="/create" element={<CreatePollPage />} />
```
The doc's table is missing the **`/` → HomePage (landing)** row and the **`/create` → CreatePollPage**
row, and its `/` row is simply incorrect. The guest "log in to create" CTA now lives at `/create`.
**Fix:** rewrite the routes table (add `/` HomePage + `/create` CreatePollPage rows).

### 2. Project-structure tree is missing 4 frontend source files  `MEDIUM`
**Doc** (§ Project Structure, frontend block) omits files that exist and matter:

| Missing from tree | What it is |
|---|---|
| [pages/HomePage.tsx](frontend/src/pages/HomePage.tsx) | the landing page (new in `2ed2db1`) |
| [hooks/useAuth.ts](frontend/src/hooks/useAuth.ts) | login/register actions hook (long-standing — never listed) |
| [hooks/useAuthStatus.ts](frontend/src/hooks/useAuthStatus.ts) | reactive auth/role state (fixes logout reactivity) |
| [api/warmup.ts](frontend/src/api/warmup.ts) | free-tier backend warm-up pings on app load |

Also absent (cosmetic): `public/` (favicon.svg, icons.svg), `index.html`, `assets/`.
**Fix:** add the 4 files to the tree (the cosmetic ones optional).

### 3. Project-structure tree is missing 3 test files  `MEDIUM`
**Doc** lists per-service test folders but omits files that exist:
- vote-api: `Integration/QuestionEndpointTests.cs`, `Services/QuestionServiceTests.cs`
- identity-api: `Integration/AdminUsersEndpointTests.cs`
**Fix:** add them (or state the test lists are representative, not exhaustive).

### 4. Tech-stack row "Charts — Hand-rolled SVG (`LiveBarChart`, `LineChart`)" is half wrong  `MEDIUM`
**Doc** (~line 76) claims both charts are hand-rolled **SVG**.
**Code:** [LineChart.tsx](frontend/src/components/LineChart.tsx) is SVG ✓, but
[LiveBarChart.tsx](frontend/src/components/LiveBarChart.tsx) contains **no `<svg>` at all** — it is a
**div/CSS bar chart** (`.bar-track`/`.bar-fill`, animated via `transform: scaleX`).
**Fix:** "Charts — hand-rolled: CSS/div bar chart (`LiveBarChart`) + SVG line chart (`LineChart`)".

### 5. Production frontend hosting no longer matches "Frontend Web Service"  `MEDIUM`
**Doc** (§ Production (Render), ~line 690): lists a "Frontend Web Service" pulling the
`pollbuilder-frontend` Docker image (nginx proxying to the gateway).
**Reality:** the production frontend is a **Render Static Site** (`poll-service-tj07.onrender.com`) —
built from the repo with `VITE_API_URL`/`VITE_HUB_URL` baked to the gateway's public URL, calling the
gateway **cross-origin** (gateway `Frontend__Url` = the static-site origin for CORS). The old frontend
Web Service was retired. Consequences the doc doesn't reflect:
- The nginx-proxy/relative-URL paragraph (~line 683) describes **local docker-compose only**, not prod.
- CI still builds/pushes `pollbuilder-frontend` and has a `RENDER_HOOK_FRONTEND` deploy step — now
  vestigial for prod (the Static Site auto-deploys from git); the doc presents them as the prod path.
**Fix:** update § Production (Render) to "Static Site (frontend) + 4 Web Services + DB", note the
CORS/VITE_* arrangement, and mark the frontend image/hook as compose-only/legacy.

### 6. Documented expiry auto-close behavior currently fails in production  `MEDIUM` *(tracked)*
**Doc** (Design Decisions + § Poll API): `PollCleanupService` "auto-closes expired polls".
**Reality:** [KNOWN_ISSUES.md → ISSUE-001](KNOWN_ISSUES.md) — expired polls do **not** close in the
deployed environment (leading hypothesis: the hosted-service sweep can't run while the free-tier
instance sleeps). The code matches the doc; the **observed behavior** doesn't.
**Fix:** none needed in the doc until ISSUE-001 is resolved, but worth a footnote that the sweep
requires a running instance (free-tier caveat).

### 7. Tree comment for QuestionsController omits `delete`  `LOW`
**Doc** tree (~line 193): `QuestionsController.cs ← Anonymous Q&A (list/ask/upvote/pin)`.
**Code:** the controller also has `DELETE {code}/questions/{id}` (owner/admin). The endpoint **is**
correctly listed in the API-endpoints table — only this tree comment is stale.
**Fix:** `(list/ask/upvote/pin/delete)`.

### 8. Gateway file list omits `appsettings.Development.json`  `LOW`
**Doc** tree lists only `Program.cs` + `appsettings.json` for the gateway.
**Code:** [appsettings.Development.json](services/gateway/Gateway/appsettings.Development.json) exists
and overrides the three cluster addresses to `http://localhost:5001/5002/5003` for non-docker local
runs. This also softens the topology claim "services call each other by Docker service name, **never
by `localhost`**" (~line 139) — true in docker/prod, intentionally overridden for bare-metal dev.
**Fix:** add the file to the tree + one clause about the dev override.

### 9. System Overview wording predates multiple question types  `LOW`
**Doc** (~line 11): "A creator writes a **multiple-choice** question with up to 6 options…".
**Code:** four types (SingleChoice / YesNo / Rating / OpenText) — correctly documented further down,
but the opening sentence undersells it and "up to 6 options" only applies to SingleChoice.
**Fix:** "...writes a question (multiple-choice, yes/no, 1–5 rating, or open text)...".

### 10. Frontend warm-up behavior is undocumented  `LOW` *(gap, not contradiction)*
**Code:** [warmup.ts](frontend/src/api/warmup.ts) fires fire-and-forget pings (`/auth/warmup`,
`/polls/warmup`, `/polls/warmup/results`) on app load to wake the free-tier backend, and the login page
shows a "server is waking" hint. The doc never mentions this flow (these intentionally-404 requests
would puzzle anyone reading gateway logs against the doc).
**Fix:** one line under Data Flows or Deployment ("cold-start mitigation").

### 11. Root-level file listing omits sibling docs  `LOW` *(cosmetic)*
**Doc** tree ends with `docker-compose.yml`, `ARCHITECTURE.md`, `README.md`. The repo root also holds
`DEPLOYMENT.md`, `KNOWN_ISSUES.md`, `CONTRIBUTING.md`, `todo.md`, `docs/`, and `frontend/`'s design
docs (`REDESIGN.md`, `UItodo.md`, `design-system/`, `uiexam/`). Fine to abbreviate — but
`KNOWN_ISSUES.md`/`DEPLOYMENT.md` are referenced from other docs and deserve a line.

---

## ✅ Verified aligned (spot-checked against source — no action)

| Doc claim | Verified against | Result |
|---|---|---|
| Gateway routing table — all 13 routes, orders 1–12+100, policies, clusters | [gateway appsettings.json](services/gateway/Gateway/appsettings.json) | exact match |
| Code transform strips + sets `X-User-Id`/`X-User-Role`; `MapInboundClaims=false`; `authenticated` + `admin` policies; CORS w/ credentials | [gateway Program.cs](services/gateway/Gateway/Program.cs) | exact match |
| JWT: 7-day expiry, claims `sub`/`email`/`role`/`jti` | [AuthService.cs](services/identity-api/IdentityApi/Services/AuthService.cs) (`TokenLifetime = FromDays(7)`) | match |
| Poll API endpoints (create/get/close/delete/my-polls + admin list) incl. auth semantics | `PollsController` + `AdminPollsController` attributes | match |
| Vote API endpoints (vote/results/analytics/questions list-ask-upvote-pin-**delete**) + hub `/hubs/poll` | `VotesController`, `QuestionsController`, `Program.cs MapHub` | match |
| Identity endpoints (register/login + admin users list/role/delete) | `AuthController`, `AdminUsersController` | match |
| All 5 entities' properties (incl. `User.Role`, `QuestionUpvote.VoterKey`) | `Models/*.cs` across services | match |
| All 10 indexes incl. unique `(PollCode,VoterToken)` and `(QuestionId,VoterKey)` | the three DbContexts | match |
| 5-char poll code; `PollCleanup:IntervalSeconds` (default 60) | `PollService.CodeLength`, `PollCleanupService` | match |
| compose: only 5000 + 5173 published; 6 services + `sqldata` volume | [docker-compose.yml](docker-compose.yml) | match |
| CI: lint-and-test → build-and-push (5 images) → deploy (env-guarded hooks) | [.github/workflows/ci-cd.yml](.github/workflows/ci-cd.yml) | match |
| Per-service layering + exceptions (identity has no repo layer; gateway config-only; vote has Hubs + PollClientService) | folder inventories | match |
| Auth flow: token in `localStorage['token']`, axios interceptor, SPA-side JWT decode for UX only | `session.ts`, `api.ts` | match |
| Env-var names incl. `Admin__Emails__0`, `Services__PollApi`, `VITE_*` defaults | appsettings + `.env` | match |
| RBAC matrix + layered enforcement + admin bootstrap | controllers/services/guards (verified during Phase 12) | match |

---

## Suggested remediation order
1. **#1 routes table** (factually wrong — quick fix, high reader impact)
2. **#2/#3/#7** tree updates (one editing pass over Project Structure)
3. **#5** production-hosting section (reflects the real deployment)
4. **#4, #8, #9, #10, #11** wording/gap touch-ups (one small pass)
5. **#6** resolves itself with ISSUE-001's fix

> **Resolution (2026-06-10):** all findings #1–#11 were applied to [ARCHITECTURE.md](ARCHITECTURE.md)
> in a sync pass — routes table (#1), structure-tree files + tests + gateway dev config (#2/#3/#8),
> charts row (#4), Production-Render static-site rewrite + nginx scoping (#5), cleanup-service free-tier
> caveat linking ISSUE-001 (#6), Q&A delete comment (#7), overview wording (#9), cold-start-mitigation
> subsection (#10), and root sibling docs (#11). #6's *behavioral* fix still depends on ISSUE-001.
> This audit file is retained as the record of what was changed.
