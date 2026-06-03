# Poll & Survey Builder

A real-time, microservices-based polling platform built for the **AMD201 — Advanced Microservices Deployment** coursework (Topic 02: *Poll & Survey Builder*).

Create a poll, share a short link (e.g. `/poll/7fGh2`), and watch votes update **live** on a results bar chart via SignalR WebSockets — no page refresh. No login is required to vote; creators can sign in to manage their polls and view analytics.

**🌐 Live demo:** https://pollbuilder-frontend-latest.onrender.com/

---

## Features

**Core**
- Create a poll with 2–6 options and an optional expiry time
- Share a 5-character short code / link
- Vote once per browser (session-token dedup — no account needed)
- **Live results** over SignalR — the bar chart updates the moment anyone votes
- Creator accounts: register / login (JWT), "my polls" dashboard, close & delete

**Merit & Distinction extensions**
- ⏲️ **Poll expiry auto-close** *(Merit)* — a background service closes expired polls automatically; the results page shows a "closed — final results" banner
- 🧩 **Multiple question types** *(Merit)* — single-choice, yes/no, 1–5 rating, and open-text (free-text responses stored, not tallied)
- 📊 **Creator analytics dashboard** *(Distinction)* — votes over time (line chart), peak voting minute, and top option
- 💬 **Anonymous Q&A** *(Distinction)* — audience submits questions during a poll; everyone can upvote and the host can pin, all live over SignalR

---

## Architecture at a glance

Microservices behind an API Gateway, each owning its own database. The frontend only ever talks to the Gateway.

| Service | Tech | Responsibility |
|---|---|---|
| API Gateway | ASP.NET Core 10 + YARP | Routing, JWT validation, `X-User-Id` injection, WebSocket proxy |
| Poll API | ASP.NET Core 10 | Poll CRUD + expiry auto-close background service |
| Vote API | ASP.NET Core 10 + SignalR | Voting, results, analytics, Q&A, live broadcast |
| Identity API | ASP.NET Core 10 | Register / login / JWT issuance |
| Frontend | React 19 + TypeScript + Vite 8 | SPA (create / vote / live results / analytics / Q&A) |
| Database | SQL Server 2022 | One database per service (`PollDb`, `VoteDb`, `IdentityDb`) |

```
Frontend (React) ──▶ API Gateway (YARP, :5000) ──┬─▶ Poll API      ─▶ PollDb
                                                  ├─▶ Vote API      ─▶ VoteDb   (SignalR hub /hubs/poll)
                                                  └─▶ Identity API  ─▶ IdentityDb
                          Vote API ──HTTP──▶ Poll API   (validate poll before accepting a vote)
```

**📖 Full architecture — structure, schema, data flows, endpoints, gateway routing, and design decisions — is in [ARCHITECTURE.md](ARCHITECTURE.md) (the authoritative source).**

---

## Tech stack

ASP.NET Core 10 · Entity Framework Core 10 · SignalR · YARP · SQL Server 2022 · React 19 · Vite 8 · TypeScript · `@microsoft/signalr` · BCrypt · JWT Bearer · Docker (multi-stage) · docker-compose · GitHub Actions · Docker Hub · Render

---

## Quick start (local, with Docker)

Requires **Docker Desktop**. See [CONTRIBUTING.md](CONTRIBUTING.md) for the full prerequisites list.

```bash
# 1. Provide secrets (gitignored) — copy the template and fill in values
cp .env.example .env
#    set SA_PASSWORD (SQL Server sa password) and JWT_SECRET (min 32 chars)

# 2. Build and start everything
docker-compose up --build
```

- **Frontend:** http://localhost:5173
- **Gateway (API):** http://localhost:5000

**Database migrations apply automatically on startup** — each service runs `Database.MigrateAsync()` with a retry loop while SQL Server initializes. There is no manual `dotnet ef database update` step (the runtime images don't ship the SDK).

Only the Gateway (5000) and Frontend (5173) publish host ports; the backend services and SQL Server stay on the internal Docker network.

### Running without Docker (per service)

```bash
# Each backend service (uses User Secrets / appsettings for the connection string)
dotnet run --project services/poll-api/PollApi --no-launch-profile
dotnet run --project services/vote-api/VoteApi --no-launch-profile
dotnet run --project services/identity-api/IdentityApi --no-launch-profile
dotnet run --project services/gateway/Gateway --no-launch-profile

# Frontend
cd frontend && npm install && npm run dev
```

> Local secrets (DB password, `Jwt:Secret`) live in **.NET User Secrets**, never in git. The committed `appsettings.json` files hold `__SET_VIA_USER_SECRETS_OR_ENV__` placeholders.

---

## Tests

```bash
dotnet test services/poll-api/PollApi.sln          # 26 tests
dotnet test services/vote-api/VoteApi.sln          # 35 tests
dotnet test services/identity-api/IdentityApi.sln  # 13 tests
cd frontend && npm run lint && npm run build       # eslint + typecheck + build
```

**74 backend tests** (unit + integration via `WebApplicationFactory` + EF in-memory) plus a green frontend lint/build. The same suite runs in CI on every push and PR.

---

## Deployment

Push to `main` → GitHub Actions lints & tests all services, builds 5 multi-stage Docker images, pushes them to Docker Hub, and triggers a Render deploy per service.

See **[DEPLOYMENT.md](DEPLOYMENT.md)** for the GitHub secrets, Docker Hub token, Render service setup, and production environment variables.

---

## Repository layout

```
poll-service/
├── services/            # Backend microservices
│   ├── poll-api/        #   Poll CRUD + expiry auto-close
│   ├── vote-api/        #   Voting, results, analytics, Q&A, SignalR hub
│   ├── identity-api/    #   Register / login / JWT
│   └── gateway/         #   YARP API Gateway (single entry point)
├── frontend/            # React 19 + Vite SPA
├── .github/workflows/   # CI/CD pipeline (ci-cd.yml)
├── ARCHITECTURE.md      # Authoritative architecture & design
├── DEPLOYMENT.md        # Cloud deploy + CI/CD setup
├── todo.md              # Phased build plan (Pass / Merit / Distinction)
├── CONTRIBUTING.md      # Workflow, conventions, prerequisites
└── docker-compose.yml   # Local orchestration
```

---

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) — system design, schema, data flows, endpoints, deployment (authoritative)
- [DEPLOYMENT.md](DEPLOYMENT.md) — CI/CD pipeline + cloud provisioning
- [todo.md](todo.md) — phased build plan with Pass / Merit / Distinction tiers
- [CONTRIBUTING.md](CONTRIBUTING.md) — branch strategy, commit conventions, local setup

---

## License

Coursework project — for academic assessment (AMD201).
