# ARCHITECTURE.md ↔ Codebase Alignment Audit

**Date:** 2026-07-03 · **Doc audited:** [ARCHITECTURE.md](ARCHITECTURE.md) (HEAD `148c6ab`)
**Method:** every checkable claim in the doc was verified against the actual source — gateway config,
controller route attributes, models, DbContext index definitions, `Program.cs` files, docker-compose,
the Dockerfiles, the CI workflow, the frontend `src/` inventory + `App.tsx` routes, styling/config
claims, and the root docs + `pollbuilder-*` skills.

**Verdict:** the codebase and doc are in excellent shape — backend, frontend, and CI/CD sections all
match the code exactly, and the "Redesign all core features UI" commit (`148c6ab`) introduced **no
structural drift**. One real defect was found (an undocumented, unset `GATEWAY_URL` breaking the
compose frontend) plus two minor doc gaps. All three were fixed in this sync pass.

---

## ❌ Findings

### 1. `GATEWAY_URL` never set for the frontend container — compose proxy broken  `HIGH`
**Doc** (§ docker-compose): "Nginx in the frontend container proxies `/api/` and `/hubs/` to
`gateway:8080`".
**Code:** [nginx.conf:26](frontend/nginx.conf#L26) / [:38](frontend/nginx.conf#L38) use
`proxy_pass ${GATEWAY_URL}/...;` and the [Dockerfile](frontend/Dockerfile#L20) installs the file as
`/etc/nginx/templates/default.conf.template`, so nginx's entrypoint substitutes `${GATEWAY_URL}` via
envsubst at container start. But `GATEWAY_URL` appeared **nowhere else in the repo** — the frontend
service in [docker-compose.yml](docker-compose.yml) had no `environment:` block, and neither
ARCHITECTURE.md nor DEPLOYMENT.md mentioned the variable. An unset variable substitutes to empty,
yielding `proxy_pass /api/;` (no scheme) — an invalid nginx config, so the frontend container fails
in compose mode. (The template + SNI comments suggest it was written for the legacy Render
web-service path, where `GATEWAY_URL` was set in the Render dashboard — which is why compose never
got it.)
**Fix (applied):** `GATEWAY_URL: "http://gateway:8080"` added to the frontend service environment in
docker-compose.yml; variable documented in ARCHITECTURE.md's Environment Configuration table.

### 2. `GATEWAY_URL` / template mechanism undocumented  `MEDIUM` *(gap)*
The doc's Environment Configuration table had no Frontend `GATEWAY_URL` row and never stated that
`nginx.conf` is an envsubst template requiring the variable at runtime.
**Fix (applied):** table row added; the tree's `nginx.conf` comment now notes the template mechanism.

### 3. Root-level tree omits sibling docs the doc itself references  `LOW` *(cosmetic)*
The project-structure tree ended at `README.md` while the repo root also holds `PRODUCT.md` /
`DESIGN.md` (referenced from the design-decisions table), `todo.md`, `docs/`, and this audit file.
**Fix (applied):** one-line entries added to the tree.

---

## ✅ Verified aligned (checked against source — no action)

| Doc claim | Verified against | Result |
|---|---|---|
| Gateway routing table — all 13 routes, orders 1–12+100, policies, clusters; dev overrides → localhost:5001/5002/5003 | gateway `appsettings.json` + `appsettings.Development.json` | exact match |
| Code transform strips + sets `X-User-Id`/`X-User-Role`; `authenticated` + `admin` policies; CORS w/ credentials | gateway `Program.cs` | exact match |
| Poll API endpoints (create/get/close/delete/my-polls + admin list) incl. owner/admin semantics | `PollsController`, `AdminPollsController` | match |
| Vote API endpoints (vote/results/analytics/questions list-ask-upvote-pin-delete) + hub `/hubs/poll`, upvote repeat → 409 | `VotesController`, `QuestionsController`, `PollHub`, `Program.cs` | match |
| Identity endpoints (register/login + admin users list/role/delete, self-change blocked) | `AuthController`, `AdminUsersController` | match |
| All 6 entities' columns/max-lengths/defaults incl. `Vote.AuthorName`/`AuthorRole`, `QuestionUpvote` | `Models/*.cs` + DbContexts + migrations | match |
| All 10 indexes incl. unique `(PollCode,VoterToken)` and `(QuestionId,VoterKey)` | the three DbContexts | match |
| JWT: 7-day expiry, `sub`/`email`/`role`/`jti` claims; admin bootstrap from `Admin:Emails` | `AuthService.cs`, identity `Program.cs` | match |
| `PollCleanupService` sweep + lazy close-on-read in `GetByCodeAsync`; auto-migrations w/ retry; `Result<T>`; `ErrorHandlingMiddleware` | poll/vote/identity `Program.cs` + services | match |
| Inter-service: typed `HttpClient` `PollClientService`, `Services__PollApi`, reject vote when Poll API down | vote-api `PollClientService`, `VoteService` | match |
| Frontend: all 9 routes + `RequireAuth`/`RequireAdmin` guards; landing full-bleed BoardNav/BoardFooter vs centered legacy chrome; `ToastProvider` mounted | `App.tsx` | match |
| All 10 hooks + all documented components exist and do their described jobs; no undocumented extras | `src/hooks/`, `src/components/` inventories | match |
| axios instance + auth interceptor + 401 handling; warmup pings (404 by design); `session.ts` decode + `auth-change`; persistent voter token; client-side CSV w/ BOM; SignalR join/leave + `ReceiveVoteUpdate`/`ReceiveQuestionsUpdate` | `api.ts`, `warmup.ts`, `session.ts`, `voter.ts`, `csv.ts`, `useLiveResults.ts`, `useQuestions.ts` | match |
| Tailwind v4 `@theme` + legacy `index.css` in lowest cascade layer; dark-only (`data-theme="dark"`, no `useTheme`); Bricolage/Hanken/Geist Mono; qrcode.react w/ white quiet zone | `tailwind.css`, `index.css`, `index.html`, `main.tsx`, `ShareLink.tsx` | match |
| Stack versions: React 19, Vite 8, Tailwind 4, `@microsoft/signalr`, qrcode.react | `package.json` | match |
| compose: only 5000 + 5173 published; db healthcheck gates `depends_on`; `${SA_PASSWORD}`/`${JWT_SECRET}` interpolation; per-service env vars | [docker-compose.yml](docker-compose.yml) | match (after fix #1) |
| All 5 Dockerfiles multi-stage; backend EXPOSE 8080; frontend nginx + build args | `services/*/Dockerfile`, `frontend/Dockerfile` | match |
| CI: lint-and-test (3 solutions + frontend) → build-and-push (5 `pollbuilder-*` images, GHA cache) → env-guarded Render hooks | [.github/workflows/ci-cd.yml](.github/workflows/ci-cd.yml) | match |
| KNOWN_ISSUES: ISSUE-001 marked Fixed (2026-06-10) as the doc claims | [KNOWN_ISSUES.md](KNOWN_ISSUES.md) | match |
| `pollbuilder-*` skills stay reusable-only and defer to ARCHITECTURE.md; no hardcoded contradictions | `.claude/skills/pollbuilder-*/SKILL.md` | match |

---

> **Resolution (2026-07-03):** finding #1 fixed in [docker-compose.yml](docker-compose.yml)
> (`GATEWAY_URL` env var on the frontend service); findings #2/#3 fixed in
> [ARCHITECTURE.md](ARCHITECTURE.md) (env-table row, `nginx.conf` template note, root-tree entries).
> Previous audit (2026-06-10, HEAD `b948885`) found 11 doc-drift items after the Mentimeter redesign;
> all were resolved in that sync pass — this audit supersedes it as the current record.
