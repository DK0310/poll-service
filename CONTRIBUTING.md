# Contributing & Development Workflow

Team conventions for the Poll & Survey Builder (AMD201). Read this before your first commit.

---

## Prerequisites

Install locally:

| Tool | Required version | Notes |
|---|---|---|
| .NET SDK | **10.0** | All services target `net10.0`. |
| Node.js | **20 LTS or newer** | For the React/Vite frontend (Node 22 is fine). |
| Docker Desktop | latest | For `docker-compose` and image builds. |
| EF Core CLI | 10.x | `dotnet tool install --global dotnet-ef` |
| Git | latest | — |

> **Target framework — decided (Phase 0):** the project targets **`net10.0`** to match the installed dev toolchain (.NET SDK 10). [ARCHITECTURE.md](ARCHITECTURE.md) and the Dockerfiles (`dotnet/sdk:10.0` / `dotnet/aspnet:10.0`) reflect this. Node 20 LTS is the baseline; Node 22 (installed here) works fine. No `global.json` is pinned, so any installed .NET 10 SDK works.

---

## Solution structure decision

**One `.sln` per service** (`PollApi.sln`, `VoteApi.sln`, `IdentityApi.sln`, `Gateway.sln`), not a single root solution.

**Why:** each microservice builds, tests, and deploys independently — matching the database-per-service architecture and the per-service CI/CD jobs. A root solution would couple them and blur the boundaries.

---

## Branch strategy

```
main  ← always deployable; protected; every push triggers CI/CD
  └── feature/<short-description>   e.g. feature/poll-api-crud
  └── fix/<short-description>       e.g. fix/duplicate-vote-409
```

- **Never commit directly to `main`.** Branch, push, open a Pull Request.
- A PR must pass CI (lint + tests) before merge.
- Keep PRs scoped to one phase/feature where possible.
- Delete the branch after merge.

---

## Commit conventions

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

feat(poll-api): add POST /api/polls endpoint
fix(vote-api): return 409 on duplicate vote
test(poll-api): cover option-count validation
chore(ci): add frontend lint step
docs(architecture): document SignalR flow
```

**Types:** `feat`, `fix`, `test`, `chore`, `docs`, `refactor`, `style`, `perf`.
**Scopes:** `poll-api`, `vote-api`, `identity-api`, `gateway`, `frontend`, `ci`, `docker`, `architecture`.

---

## Local secrets (.NET User Secrets)

**No real secrets live in committed files.** `appsettings.json` ships with non-secret placeholders (e.g. `Password=__SET_VIA_USER_SECRETS_OR_ENV__`). Supply real local values via **User Secrets** (stored in your Windows user profile, never in the repo); production reads from environment variables (docker-compose / Render).

First-time local setup per backend service (UserSecretsId is already in each `.csproj`):

```bash
# Poll API
dotnet user-secrets set "ConnectionStrings:Default" \
  "Server=localhost,1433;Database=PollDb;User Id=sa;Password=<your-local-pw>;TrustServerCertificate=True;" \
  --project services/poll-api/PollApi

# Vote API
dotnet user-secrets set "ConnectionStrings:Default" \
  "Server=localhost,1433;Database=VoteDb;User Id=sa;Password=<your-local-pw>;TrustServerCertificate=True;" \
  --project services/vote-api/VoteApi

# Identity API — added in Phase 6 (ConnectionStrings:Default + Jwt:Secret)
```

- User Secrets load **only in the Development environment**. `dotnet run` defaults to Development locally and picks them up automatically.
- For EF commands against a real local DB, run in Development so secrets load:
  `ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --project services/poll-api/PollApi`
  (or set `ConnectionStrings__Default` as an env var). For docker, the connection string comes from compose env vars.
- Inspect/clear: `dotnet user-secrets list --project <proj>` / `dotnet user-secrets clear --project <proj>`.

> Never commit: real DB passwords, `Jwt__Secret`, SQL Server `SA_PASSWORD`, Docker Hub credentials, Render deploy hooks. Those belong in User Secrets (local), GitHub Actions Secrets (CI), or the Render secret store (prod).

---

## Definition of Done (per task)

- Code builds and **tests pass** (`dotnet test` / `npm run lint`) — verify with real commands, don't assume.
- New behavior has a test (success **and** failure path).
- No service reaches into another service's database — cross-service data goes through HTTP.
- [ARCHITECTURE.md](ARCHITECTURE.md) updated if structure/schema/flows/endpoints changed (it is the source of truth).

---

## Working agreements

- Follow the phased plan in [todo.md](todo.md); finish a phase's **Definition of Done** before starting the next.
- Coding patterns and conventions live in `.claude/skills/pollbuilder-*` — consult the relevant skill for each layer.
- Format per [.editorconfig](.editorconfig) (4-space C#, 2-space TS/JSON/YAML).
- Secrets never go in git — use `.env` (gitignored) locally and the platform secret store in production.
