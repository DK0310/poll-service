# Poll & Survey Builder — Architecture

## System Overview

Poll Builder is a **microservices-based** real-time polling platform built for the AMD201 Advanced Microservices Deployment coursework. Users create multiple-choice polls, share a short link, and collect votes with **live results** powered by SignalR WebSockets.

---

## Architecture Diagram

```
                          ┌──────────────────┐
                          │    Frontend       │
                          │  React + Vite     │
                          │  Port 5173        │
                          └────────┬──────────┘
                                   │
                          ┌────────▼──────────┐
                          │   API Gateway      │
                          │   (YARP)           │
                          │   Port 5000        │
                          │   • JWT validation │
                          │   • Route matching │
                          └──┬──────┬──────┬──┘
             ┌───────────────┤      │      ├────────────────┐
             │               │      │      │                │
    ┌────────▼────────┐      │      │      │   ┌────────────▼────────────┐
    │ Identity API     │      │      │      │   │   Vote API              │
    │ POST /api/auth/* │      │      │      │   │   POST /polls/*/vote    │
    │ Port 5003        │      │      │      │   │   GET  /polls/*/results │
    └────────┬─────────┘      │      │      │   │   WS   /hubs/poll      │
             │                │      │      │   │   Port 5002             │
    ┌────────▼─────────┐      │      │      │   └────────────┬────────────┘
    │   IdentityDb     │      │      │      │                │
    │   (Users)        │      │      │      │   ┌────────────▼────────────┐
    └──────────────────┘      │      │      │   │   VoteDb                │
                              │      │      │   │   (Votes)               │
                     ┌────────▼──────▼──────▼┐  └─────────────────────────┘
                     │   Poll API            │
                     │   /api/polls/*        │        ─── HTTP (sync) ───▶
                     │   Port 5001           │◀──── Vote API calls Poll API
                     └────────┬──────────────┘       to validate polls
                     ┌────────▼──────────────┐
                     │   PollDb              │
                     │   (Polls, PollOptions)│
                     └───────────────────────┘
```

---

## Technology Stack

| Component | Technology | Version |
|---|---|---|
| Frontend | React + TypeScript + Vite | React 18, Vite 5 |
| API Gateway | ASP.NET Core + YARP | .NET 8 |
| Poll Service | ASP.NET Core Web API | .NET 8 |
| Vote Service | ASP.NET Core Web API + **SignalR** | .NET 8 |
| Identity Service | ASP.NET Core Web API | .NET 8 |
| Database | SQL Server (per-service DBs) | 2022 |
| ORM | Entity Framework Core | 8.0 |
| Real-Time | SignalR WebSocket | ASP.NET Core 8 |
| Charts | Chart.js or Recharts | Latest |
| Containers | Docker (multi-stage) | Latest |
| Orchestration | docker-compose | v2 |
| CI/CD | GitHub Actions | — |
| Registry | Docker Hub | — |
| Hosting | Render | — |

---

## Microservices

### 1. API Gateway (YARP)

| Property | Value |
|---|---|
| Port | 5000 (external), 8080 (container) |
| Responsibility | Route requests, validate JWT, set X-User-Id header |
| Database | None (stateless) |
| Key Tech | YARP reverse proxy |

The Gateway is the **single entry point** for all external traffic. It:
- Routes requests to the correct backend service based on URL patterns
- Validates JWT tokens for protected endpoints
- Extracts user ID from JWT and forwards it as `X-User-Id` header
- Proxies WebSocket connections for SignalR

### 2. Poll API

| Property | Value |
|---|---|
| Port | 5001 (external), 8080 (container) |
| Responsibility | Poll CRUD — create, read, close, delete, list |
| Database | `PollDb` — Polls, PollOptions tables |
| Key Endpoints | `POST /api/polls`, `GET /api/polls/{code}`, `PATCH /api/polls/{code}/close` |

### 3. Vote API

| Property | Value |
|---|---|
| Port | 5002 (external), 8080 (container) |
| Responsibility | Vote submission, results aggregation, **real-time broadcasting** |
| Database | `VoteDb` — Votes table |
| Key Endpoints | `POST /api/polls/{code}/vote`, `GET /api/polls/{code}/results` |
| Special | **SignalR Hub** at `/hubs/poll` for live vote updates |

The Vote API calls the Poll API over HTTP to validate that a poll exists and is active before accepting a vote.

### 4. Identity API

| Property | Value |
|---|---|
| Port | 5003 (external), 8080 (container) |
| Responsibility | User registration, login, JWT token generation |
| Database | `IdentityDb` — Users table |
| Key Endpoints | `POST /api/auth/register`, `POST /api/auth/login` |

---

## Database Design

### Database-Per-Service Pattern

Each service owns its data exclusively. No service queries another service's database.

```
┌─────────────────────────────────────────────────────────────┐
│                     SQL Server Instance                      │
├─────────────────┬──────────────────┬────────────────────────┤
│     PollDb      │     VoteDb       │     IdentityDb         │
├─────────────────┼──────────────────┼────────────────────────┤
│ Polls           │ Votes            │ Users                  │
│ ├─ Id (PK)      │ ├─ Id (PK)       │ ├─ Id (PK)            │
│ ├─ Code (UQ)    │ ├─ PollCode      │ ├─ Email (UQ)         │
│ ├─ Question     │ ├─ OptionIndex   │ ├─ PasswordHash       │
│ ├─ Status       │ ├─ VoterToken    │ └─ CreatedAt          │
│ ├─ ExpiresAt    │ ├─ VotedAt       │                        │
│ ├─ CreatorId    │ └─ UQ(PollCode,  │                        │
│ └─ CreatedAt    │      VoterToken) │                        │
│                 │                  │                        │
│ PollOptions     │                  │                        │
│ ├─ Id (PK)      │                  │                        │
│ ├─ PollId (FK)  │                  │                        │
│ ├─ OptionIndex  │                  │                        │
│ └─ Text         │                  │                        │
└─────────────────┴──────────────────┴────────────────────────┘
```

### Cross-Service References

- `Poll.CreatorId` stores a Guid from the JWT (NOT a FK to Users — that table is in IdentityDb)
- `Vote.PollCode` stores a string (NOT a FK to Polls — that table is in PollDb)
- Cross-service validation happens via HTTP API calls

---

## API Endpoints

### Poll API (via Gateway)

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/polls` | Optional | Create a new poll |
| GET | `/api/polls/{code}` | No | Get poll details + options |
| PATCH | `/api/polls/{code}/close` | Required (creator) | Close poll |
| DELETE | `/api/polls/{code}` | Required (creator) | Delete poll |
| GET | `/api/polls/my-polls` | Required | List creator's polls |

### Vote API (via Gateway)

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/polls/{code}/vote` | No | Submit a vote |
| GET | `/api/polls/{code}/results` | No | Get vote results |
| WS | `/hubs/poll` | No | SignalR live results |

### Identity API (via Gateway)

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/register` | No | Register new user |
| POST | `/api/auth/login` | No | Login, receive JWT |

---

## Real-Time Architecture (SignalR)

```
1. Client opens Results Page
   → Fetches initial results via GET /api/polls/{code}/results
   → Connects to SignalR hub at /hubs/poll (via Gateway)
   → Calls JoinPollGroup(pollCode) to subscribe

2. Another user votes
   → POST /api/polls/{code}/vote → Gateway → Vote API
   → VoteService saves vote to VoteDb
   → VoteService broadcasts updated results via IHubContext
   → All connected clients receive ReceiveVoteUpdate event
   → Charts update in real-time (no page refresh)

3. Client leaves page
   → Calls LeavePollGroup(pollCode)
   → Disconnects from SignalR hub
```

---

## Communication Patterns

### External Traffic (Frontend → Gateway → Service)

All external requests go through the API Gateway. The frontend only knows the Gateway URL.

### Internal Traffic (Service → Service)

The Vote API calls the Poll API directly using Docker service names:

```
Vote API  ──HTTP GET──▶  http://poll-api:8080/api/polls/{code}
```

This bypasses the Gateway — internal service-to-service calls don't need auth validation.

---

## Deployment Architecture

### Local Development

```bash
docker-compose up --build
# Starts: db, gateway, poll-api, vote-api, identity-api, frontend
# All services accessible via localhost ports
```

### Production (Render)

Each service is deployed as a separate Render Web Service pulling from Docker Hub:

```
GitHub push → GitHub Actions CI/CD
  → lint + test all services
  → build 5 Docker images → push to Docker Hub
  → trigger Render deploy webhooks (one per service)
```

### CI/CD Pipeline

```
Push to main
  │
  ├─ Phase 1: Lint & Test (all services)
  │   ├─ dotnet test services/poll-api/PollApi.sln
  │   ├─ dotnet test services/vote-api/VoteApi.sln
  │   ├─ dotnet test services/identity-api/IdentityApi.sln
  │   └─ npm run lint (frontend)
  │
  ├─ Phase 2: Build & Push Docker Images
  │   ├─ pollbuilder-gateway:latest
  │   ├─ pollbuilder-poll-api:latest
  │   ├─ pollbuilder-vote-api:latest
  │   ├─ pollbuilder-identity-api:latest
  │   └─ pollbuilder-frontend:latest
  │
  └─ Phase 3: Deploy to Render (webhook per service)
```

---

## Project Structure

```
poll-service/
├── services/
│   ├── poll-api/              ← Poll management microservice
│   │   ├── PollApi/           ← ASP.NET Core Web API
│   │   ├── PollApi.Tests/     ← xUnit tests
│   │   ├── Dockerfile
│   │   └── PollApi.sln
│   │
│   ├── vote-api/              ← Voting + real-time microservice
│   │   ├── VoteApi/           ← ASP.NET Core + SignalR
│   │   ├── VoteApi.Tests/
│   │   ├── Dockerfile
│   │   └── VoteApi.sln
│   │
│   ├── identity-api/          ← Auth microservice
│   │   ├── IdentityApi/
│   │   ├── IdentityApi.Tests/
│   │   ├── Dockerfile
│   │   └── IdentityApi.sln
│   │
│   └── gateway/               ← API Gateway (YARP)
│       ├── Gateway/
│       ├── Dockerfile
│       └── Gateway.sln
│
├── frontend/                  ← React SPA
│   ├── src/
│   ├── Dockerfile
│   └── nginx.conf
│
├── .github/workflows/
│   └── ci-cd.yml
├── docker-compose.yml
├── ARCHITECTURE.md            ← This file
└── README.md
```

---

## Environment Configuration

| Service | Key Variables |
|---|---|
| **Gateway** | `Jwt__Secret`, `Frontend__Url` |
| **Poll API** | `ConnectionStrings__Default` (PollDb) |
| **Vote API** | `ConnectionStrings__Default` (VoteDb), `Services__PollApi` |
| **Identity API** | `ConnectionStrings__Default` (IdentityDb), `Jwt__Secret` |
| **Frontend** | `VITE_API_URL` (Gateway), `VITE_HUB_URL` (SignalR via Gateway) |

> `Jwt__Secret` must be identical in Gateway and Identity API.

---

## Design Decisions

| Decision | Rationale |
|---|---|
| **Microservices over monolith** | Independent deployment, scaling, and development per domain |
| **YARP API Gateway** | .NET-native reverse proxy with built-in transforms for JWT → X-User-Id |
| **Database per service** | Data ownership, no cross-service schema dependencies |
| **SignalR in Vote API only** | Only voting needs real-time; other services use REST |
| **PollCode as string in VoteDb** | No FK across databases; validated via HTTP call to Poll API |
| **JWT at Gateway only** | Centralized auth; services trust Gateway's X-User-Id header |
| **Docker multi-stage builds** | ~200MB production images instead of ~900MB |
| **Voter deduplication via token** | Session/fingerprint-based — no login required for voters |
