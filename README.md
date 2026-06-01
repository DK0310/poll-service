# Poll & Survey Builder

A real-time, microservices-based polling platform built for the **AMD201 — Advanced Microservices Deployment** coursework (Topic 02: *Poll & Survey Builder*).

Create a multiple-choice poll, share a short link (e.g. `/poll/7fGh2`), and watch votes update **live** on a results bar chart via SignalR WebSockets — no page refresh.

> 🚧 **Status: under construction.** The repository currently contains the design docs and project scaffolding. Implementation proceeds in phases — see [todo.md](todo.md). This README will be completed in Phase 11 with full setup steps and the live deployment URL.

---

## Architecture at a glance

Microservices behind an API Gateway, each owning its own database:

| Service | Tech | Responsibility |
|---|---|---|
| API Gateway | ASP.NET Core + YARP | Routing, JWT validation, WebSocket proxy |
| Poll API | ASP.NET Core 10 | Poll CRUD |
| Vote API | ASP.NET Core 10 + SignalR | Voting, results, live broadcast |
| Identity API | ASP.NET Core 10 | Register / login / JWT |
| Frontend | React 18 + TypeScript + Vite | SPA (create / vote / live results) |
| Database | SQL Server 2022 | One database per service |

**📖 Full architecture — structure, schema, data flows, endpoints, deployment — is in [ARCHITECTURE.md](ARCHITECTURE.md) (the authoritative source).**

---

## Tech stack

ASP.NET Core 10 · Entity Framework Core 8 · SignalR · SQL Server 2022 · React 18 · Vite · TypeScript · Docker (multi-stage) · docker-compose · GitHub Actions · Docker Hub · Render

---

## Quick start (once implemented)

```bash
# Requires Docker Desktop (see CONTRIBUTING.md for the full prerequisites list)
docker-compose up --build

# Apply database migrations once SQL Server is ready
docker-compose exec poll-api     dotnet ef database update
docker-compose exec vote-api     dotnet ef database update
docker-compose exec identity-api dotnet ef database update

# Frontend:  http://localhost:5173
# Gateway:   http://localhost:5000
```

---

## Repository layout

```
poll-service/
├── services/            # Backend microservices (poll-api, vote-api, identity-api, gateway)
├── frontend/            # React + Vite SPA
├── .github/workflows/   # CI/CD pipeline
├── ARCHITECTURE.md      # Authoritative architecture & design
├── todo.md              # Phased build plan
├── CONTRIBUTING.md      # Workflow, conventions, prerequisites
└── docker-compose.yml   # Local orchestration (added in Phase 7)
```

---

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) — system design, schema, data flows, deployment (authoritative)
- [todo.md](todo.md) — phased build plan with Pass / Merit / Distinction tiers
- [CONTRIBUTING.md](CONTRIBUTING.md) — branch strategy, commit conventions, local setup

---

## License

Coursework project — for academic assessment (AMD201).
