# Deployment Guide

The CI/CD pipeline ([.github/workflows/ci-cd.yml](.github/workflows/ci-cd.yml)) runs automatically on every push to `main`:

1. **Lint & Test** — `dotnet test` for all three backend services + frontend `npm ci` / `lint` / `build`. Runs on pushes **and** PRs.
2. **Build & Push** (main only) — builds the 5 multi-stage Docker images and pushes them to Docker Hub (with GitHub Actions layer cache).
3. **Deploy** (main only) — POSTs each service's Render deploy webhook. Each step **no-ops until its secret is set**, so the pipeline stays green while you wire deployment up incrementally.

> **Why a non-deployed app matters:** the brief caps a non-deployed submission at grade 4. The live URL must be reachable before presentation day.

---

## One-time setup (requires your accounts)

These steps need a GitHub repo, a Docker Hub account, and a Render account — they can't be automated from here.

### 1. Push the repo to GitHub
Create the repo and push `main`. The workflow triggers on the first push (the Build/Deploy jobs only run on `main`).

### 2. Create a Docker Hub access token
Docker Hub → Account Settings → Security → New Access Token (Read/Write).

### 3. Add GitHub Actions secrets
Repo → Settings → Secrets and variables → Actions → **New repository secret**:

| Secret | Value |
|---|---|
| `DOCKERHUB_USERNAME` | your Docker Hub username |
| `DOCKERHUB_TOKEN` | the access token from step 2 |
| `RENDER_HOOK_POLL_API` | Render deploy hook for poll-api (step 5) |
| `RENDER_HOOK_VOTE_API` | Render deploy hook for vote-api |
| `RENDER_HOOK_IDENTITY_API` | Render deploy hook for identity-api |
| `RENDER_HOOK_GATEWAY` | Render deploy hook for gateway |
| `RENDER_HOOK_FRONTEND` | Render deploy hook for frontend |

Until the Docker Hub secrets exist, the **Build & Push** job fails (expected). Until a `RENDER_HOOK_*` exists, that single **Deploy** step is skipped.

### 4. Provision a SQL Server database
Create one SQL Server reachable from Render (a managed instance, a SQL container on Render, or an external provider). Note the host, and create/allow the three databases `PollDb`, `VoteDb`, `IdentityDb` (the services auto-create their schema on startup via EF migrations).

> **RBAC migrations apply automatically.** Phase 12 adds `Users.Role` (IdentityDb, `AddUserRole`) and the `QuestionUpvotes` table (VoteDb, `AddQuestionUpvotes`). Both run on the next startup via `Database.MigrateAsync()` — no manual step. After deploy, set `Admin__Emails__0` on identity-api to your account's email and restart it once to be promoted to **Admin**.

### 5. Create 5 Render Web Services (Docker image deploys)
For each service, create a Render Web Service that deploys the pushed image `docker.io/<DOCKERHUB_USERNAME>/pollbuilder-<service>:latest`, then copy its **Deploy Hook URL** into the matching GitHub secret (step 3). Set environment variables per service:

| Render service | Image | Env vars (production values — set in Render, never in git) |
|---|---|---|
| poll-api | `pollbuilder-poll-api` | `ConnectionStrings__Default` (PollDb) |
| vote-api | `pollbuilder-vote-api` | `ConnectionStrings__Default` (VoteDb), `Services__PollApi` (internal poll-api URL), `Gateway__Url` |
| identity-api | `pollbuilder-identity-api` | `ConnectionStrings__Default` (IdentityDb), `Jwt__Secret`, `Admin__Emails__0` (email to seed as the first **Admin** on startup; add `__1`, `__2`, … for more) |
| gateway | `pollbuilder-gateway` | `Jwt__Secret` (**identical to identity-api**), `Frontend__Url` (deployed frontend origin), and cluster addresses for poll/vote/identity (override `ReverseProxy__Clusters__*__Destinations__default__Address` to the services' internal Render URLs) |
| frontend | `pollbuilder-frontend` | none at runtime (the Gateway URL is baked at build time; the image's Nginx proxies `/api` & `/hubs` to the gateway) |

> **Gateway clusters in production:** the committed `appsettings.json` uses docker-compose service names (`http://poll-api:8080`). On Render, override each cluster address via env var to the services' reachable URLs, or run them on a shared private network. See [ARCHITECTURE.md → Gateway Routing Table](ARCHITECTURE.md).

> **Frontend → Gateway in production:** the frontend image serves the SPA and proxies `/api`/`/hubs` to `http://gateway:8080` (Nginx `proxy_pass`). On Render, point that `proxy_pass` at the gateway's internal URL (adjust `nginx.conf` / make it an env-substituted template) or host the gateway behind the same domain.

### 6. Verify
- Push a trivial commit to `main` → the Actions run goes green (test → build/push → deploy).
- Open the deployed **frontend** URL → create a poll → vote → watch live results.

---

## Required GitHub secrets (summary)

`DOCKERHUB_USERNAME`, `DOCKERHUB_TOKEN`, `RENDER_HOOK_POLL_API`, `RENDER_HOOK_VOTE_API`, `RENDER_HOOK_IDENTITY_API`, `RENDER_HOOK_GATEWAY`, `RENDER_HOOK_FRONTEND`.

Production secrets (DB connection strings, `Jwt__Secret`) live in **Render's environment**, not in git.
