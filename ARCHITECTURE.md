# Poll & Survey Builder — Architecture

> **This document is the authoritative source for this project's structure, data flows, schema, topology, and architectural decisions.**
>
> The `.claude/skills/pollbuilder-*` skills cover *reusable* patterns, conventions, decision trees, and checklists. Whenever a skill needs a concrete project fact — a folder path, a port, a table column, an endpoint, an environment variable, a deploy target — it defers to this file. If something here disagrees with a skill, this file wins.

---

## System Overview

Poll & Survey Builder is a **microservices-based** real-time polling platform built for the AMD201 Advanced Microservices Deployment coursework. A creator writes a multiple-choice question with up to 6 options, shares a short link (e.g. `/poll/7fGh2`), and collects votes. The results page shows a **live bar chart** that updates in real time via SignalR WebSockets — no page refresh.

**Architecture style:** Microservices behind an API Gateway. Each service owns its domain, its database, and its deployment lifecycle, and is independently deployable. Services talk to each other with synchronous HTTP only when necessary.

**The Golden Rule:** Each service owns its data and its domain. No service ever touches another service's database — cross-service data is fetched via HTTP API calls.

---

## Architecture Diagram

```
                          ┌──────────────────┐
                          │    Frontend       │
                          │  React + Vite     │
                          │  Port 5173        │
                          └────────┬──────────┘
                                   │ HTTP / WebSocket
                          ┌────────▼──────────┐
                          │   API Gateway      │
                          │   (YARP)           │
                          │   Port 5000        │
                          │   • JWT validation │
                          │   • Route matching │
                          │   • X-User-Id      │
                          └──┬──────┬──────┬──┘
             ┌───────────────┤      │      ├────────────────┐
             │               │      │      │                │
    ┌────────▼────────┐      │      │      │   ┌────────────▼────────────┐
    │ Identity API     │      │      │      │   │   Vote API              │
    │ POST /api/auth/* │      │      │      │   │   POST /polls/*/vote    │
    │ Port 5003        │      │      │      │   │   GET  /polls/*/results │
    └────────┬─────────┘      │      │      │   │   WS   /hubs/poll       │
             │                │      │      │   │   Port 5002             │
    ┌────────▼─────────┐      │      │      │   └────────────┬────────────┘
    │   IdentityDb     │      │      │      │                │
    │   (Users)        │      │      │      │   ┌────────────▼────────────┐
    └──────────────────┘      │      │      │   │   VoteDb                │
                              │      │      │   │   (Votes, Questions)    │
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
| Frontend | React + TypeScript + Vite | React 19, Vite 8 |
| API Gateway | ASP.NET Core + YARP | .NET 10 |
| Poll Service | ASP.NET Core Web API | .NET 10 |
| Vote Service | ASP.NET Core Web API + **SignalR** | .NET 10 |
| Identity Service | ASP.NET Core Web API | .NET 10 |
| Database | SQL Server (per-service DBs) | 2022 |
| ORM | Entity Framework Core | 10.0 |
| Real-Time | SignalR WebSocket | ASP.NET Core 10 |
| Auth | JWT Bearer (7-day expiry, validated at Gateway) | — |
| Charts | Hand-rolled SVG (`LiveBarChart`, `LineChart`) | — |
| SignalR client | `@microsoft/signalr` | Latest |
| Password hashing | BCrypt | — |
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
| Responsibility | Route requests, validate JWT, set `X-User-Id` header, proxy WebSockets |
| Database | None (stateless) |
| Key Tech | YARP reverse proxy |

The Gateway is the **single entry point** for all external traffic. It:
- Routes requests to the correct backend service based on URL patterns
- Validates JWT tokens for protected endpoints
- Extracts the user ID from the JWT and forwards it as the `X-User-Id` header
- Proxies WebSocket connections for SignalR

### 2. Poll API

| Property | Value |
|---|---|
| Port | 5001 (external), 8080 (container) |
| Responsibility | Poll CRUD — create, read, close, delete, list |
| Database | `PollDb` — Polls, PollOptions tables |
| Owns | Polls, PollOptions |
| Consumes | Nothing — self-contained |

### 3. Vote API

| Property | Value |
|---|---|
| Port | 5002 (external), 8080 (container) |
| Responsibility | Vote submission, results aggregation, **real-time broadcasting**, creator analytics, anonymous Q&A |
| Database | `VoteDb` — Votes, Questions tables |
| Owns | Votes, Questions |
| Consumes | Calls Poll API over HTTP to validate a poll exists and is active before accepting a vote |
| Special | **SignalR Hub** at `/hubs/poll` for live vote updates (`ReceiveVoteUpdate`) and live Q&A (`ReceiveQuestionsUpdate`) |

### 4. Identity API

| Property | Value |
|---|---|
| Port | 5003 (external), 8080 (container) |
| Responsibility | User registration, login, JWT token generation |
| Database | `IdentityDb` — Users table |
| Owns | Users |
| Consumes | Nothing — self-contained |

---

## Service Topology & Ports

Services call each other by **Docker service name** (e.g. `http://poll-api:8080`), never by `localhost`. Only the Gateway and Frontend are meant to be reachable from outside.

| Service | Local Port | Container Port | Docker Hostname | Internal URL |
|---|---|---|---|---|
| SQL Server | 1433 | 1433 | `db` | `db:1433` |
| API Gateway | 5000 | 8080 | `gateway` | `http://gateway:8080` |
| Poll API | 5001 | 8080 | `poll-api` | `http://poll-api:8080` |
| Vote API | 5002 | 8080 | `vote-api` | `http://vote-api:8080` |
| Identity API | 5003 | 8080 | `identity-api` | `http://identity-api:8080` |
| Frontend | 5173 | 80 | `frontend` | `http://frontend:80` |

---

## Project Structure

```
poll-service/
├── services/
│   ├── poll-api/                          ← Poll management microservice
│   │   ├── PollApi/                       ← ASP.NET Core Web API
│   │   │   ├── Controllers/
│   │   │   │   └── PollsController.cs      ← Poll CRUD endpoints
│   │   │   ├── Common/
│   │   │   │   └── Result.cs               ← Result<T> (per-service)
│   │   │   ├── Services/
│   │   │   │   ├── PollService.cs          ← Business logic
│   │   │   │   └── PollCleanupService.cs   ← Background hosted service (auto-close expired)
│   │   │   ├── Repositories/
│   │   │   │   └── PollRepository.cs       ← Data access
│   │   │   ├── DTOs/
│   │   │   │   ├── CreatePollRequest.cs
│   │   │   │   └── PollResponse.cs
│   │   │   ├── Models/
│   │   │   │   ├── Poll.cs
│   │   │   │   └── PollOption.cs
│   │   │   ├── Data/
│   │   │   │   ├── PollDbContext.cs
│   │   │   │   └── Migrations/
│   │   │   ├── Middleware/
│   │   │   │   └── ErrorHandlingMiddleware.cs
│   │   │   └── Program.cs                  ← DI registration, pipeline
│   │   ├── PollApi.Tests/                  ← xUnit + Moq
│   │   │   ├── Services/PollServiceTests.cs
│   │   │   ├── Integration/PollEndpointTests.cs
│   │   │   ├── Integration/CustomWebAppFactory.cs
│   │   │   └── PollApi.Tests.csproj
│   │   ├── Dockerfile                      ← Multi-stage build
│   │   └── PollApi.sln
│   │
│   ├── vote-api/                          ← Voting + real-time microservice
│   │   ├── VoteApi/                       ← ASP.NET Core + SignalR
│   │   │   ├── Controllers/
│   │   │   │   ├── VotesController.cs      ← Vote submission + results + analytics
│   │   │   │   └── QuestionsController.cs  ← Anonymous Q&A (list/ask/upvote/pin)
│   │   │   ├── Hubs/
│   │   │   │   └── PollHub.cs              ← SignalR hub for live results + Q&A
│   │   │   ├── Services/
│   │   │   │   ├── VoteService.cs          ← Vote logic + analytics + broadcast
│   │   │   │   ├── QuestionService.cs      ← Q&A logic + broadcast
│   │   │   │   └── PollClientService.cs    ← HTTP client to Poll API
│   │   │   ├── Repositories/
│   │   │   │   ├── VoteRepository.cs
│   │   │   │   └── QuestionRepository.cs
│   │   │   ├── DTOs/
│   │   │   │   ├── VoteRequest.cs
│   │   │   │   ├── VoteResultsResponse.cs
│   │   │   │   ├── AnalyticsResponse.cs    ← Votes-over-time, peak, top option
│   │   │   │   └── QuestionDtos.cs         ← SubmitQuestionRequest, QuestionResponse
│   │   │   ├── Models/
│   │   │   │   ├── Vote.cs
│   │   │   │   └── Question.cs
│   │   │   ├── Data/
│   │   │   │   ├── VoteDbContext.cs
│   │   │   │   └── Migrations/
│   │   │   ├── Middleware/
│   │   │   │   └── ErrorHandlingMiddleware.cs
│   │   │   └── Program.cs
│   │   ├── VoteApi.Tests/
│   │   │   ├── Services/VoteServiceTests.cs
│   │   │   ├── Integration/VoteEndpointTests.cs
│   │   │   ├── Integration/CustomWebAppFactory.cs
│   │   │   └── VoteApi.Tests.csproj
│   │   ├── Dockerfile
│   │   └── VoteApi.sln
│   │
│   ├── identity-api/                      ← Auth microservice
│   │   ├── IdentityApi/                   ← ASP.NET Core Web API
│   │   │   ├── Controllers/
│   │   │   │   └── AuthController.cs       ← Register/login
│   │   │   ├── Common/
│   │   │   │   └── Result.cs               ← Result<T> (per-service)
│   │   │   ├── Services/
│   │   │   │   └── AuthService.cs          ← BCrypt + JWT generation
│   │   │   ├── DTOs/
│   │   │   │   └── AuthDtos.cs             ← RegisterRequest, LoginRequest, AuthResponse
│   │   │   ├── Models/
│   │   │   │   └── User.cs
│   │   │   ├── Data/
│   │   │   │   ├── IdentityDbContext.cs
│   │   │   │   └── Migrations/
│   │   │   ├── Middleware/
│   │   │   │   └── ErrorHandlingMiddleware.cs
│   │   │   └── Program.cs
│   │   ├── IdentityApi.Tests/
│   │   │   ├── Services/AuthServiceTests.cs
│   │   │   ├── Integration/AuthEndpointTests.cs
│   │   │   ├── Integration/CustomWebAppFactory.cs
│   │   │   └── IdentityApi.Tests.csproj
│   │   ├── Dockerfile
│   │   └── IdentityApi.sln
│   │
│   └── gateway/                           ← API Gateway (YARP)
│       ├── Gateway/
│       │   ├── Program.cs                  ← YARP config, JWT validation, CORS
│       │   └── appsettings.json            ← Route + cluster definitions
│       ├── Dockerfile
│       └── Gateway.sln
│
├── frontend/                              ← React SPA
│   ├── src/
│   │   ├── api/
│   │   │   └── api.ts                      ← Axios instance (→ Gateway)
│   │   ├── types/
│   │   │   └── poll.types.ts               ← TypeScript interfaces for API data
│   │   ├── hooks/
│   │   │   ├── useCreatePoll.ts            ← Poll creation
│   │   │   ├── usePollInfo.ts              ← Fetch poll by code
│   │   │   ├── useVote.ts                  ← Submit vote (option or text)
│   │   │   ├── useLiveResults.ts           ← SignalR + initial results
│   │   │   ├── useAnalytics.ts             ← Fetch creator analytics
│   │   │   ├── useQuestions.ts             ← Q&A SignalR + submit/upvote/pin
│   │   │   └── useMyPolls.ts               ← Fetch creator's polls
│   │   ├── components/
│   │   │   ├── PollForm.tsx                ← Create poll form (question + type + options)
│   │   │   ├── VoteForm.tsx                ← Vote interface (radios/rating/text by type)
│   │   │   ├── LiveBarChart.tsx            ← Animated results bar chart
│   │   │   ├── LineChart.tsx               ← SVG votes-over-time chart (analytics)
│   │   │   ├── QandAPanel.tsx              ← Anonymous Q&A panel
│   │   │   ├── PollCard.tsx                ← Poll summary card
│   │   │   └── ShareLink.tsx               ← Copyable share link
│   │   ├── pages/
│   │   │   ├── CreatePollPage.tsx          ← Poll creation interface
│   │   │   ├── VotePage.tsx                ← Voting page (by code)
│   │   │   ├── ResultsPage.tsx             ← Live results page
│   │   │   ├── AnalyticsPage.tsx           ← Creator analytics dashboard
│   │   │   ├── MyPollsPage.tsx             ← Creator's poll dashboard
│   │   │   ├── LoginPage.tsx               ← Login form
│   │   │   └── RegisterPage.tsx            ← Registration form
│   │   └── App.tsx                         ← Router setup
│   ├── .env                                ← VITE_API_URL, VITE_HUB_URL (point to Gateway)
│   ├── vite.config.ts
│   ├── package.json
│   ├── tsconfig.json
│   ├── nginx.conf                          ← SPA fallback + proxy /api and /hubs to Gateway
│   └── Dockerfile
│
├── .github/workflows/
│   └── ci-cd.yml                           ← Lint/test → build/push → deploy ALL services
├── docker-compose.yml                      ← Local orchestration (all services)
├── ARCHITECTURE.md                         ← This file (authoritative)
└── README.md
```

### Per-service internal layering

Every backend service follows the same layered structure: `Controllers/` → `Services/` → `Repositories/` → `Data/`, with `DTOs/`, `Models/`, `Common/` (holds the per-service `Result<T>`), and `Middleware/` alongside. Exceptions:
- **Vote API** adds a `Hubs/` folder for SignalR and a `PollClientService` for the inter-service HTTP call.
- **Gateway** has no Controllers/Services/Repositories — only YARP configuration.
- **Identity API** has no Repository layer (`AuthService` uses the DbContext directly).

---

## Database Design

### Database-Per-Service Pattern

Each service owns its data exclusively and has its own SQL Server database, DbContext, and migration history. No service queries another service's database.

| Service | Database | DbContext | Tables |
|---|---|---|---|
| Poll API | `PollDb` | `PollDbContext` | `Polls`, `PollOptions` |
| Vote API | `VoteDb` | `VoteDbContext` | `Votes`, `Questions` |
| Identity API | `IdentityDb` | `IdentityDbContext` | `Users` |

> In development, all three databases can live in the same SQL Server instance (same `db` container, different `Database=` values). In production they may be separate databases or separate instances. EF Core migrations create each database independently.

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
│ ├─ Type         │ ├─ TextAnswer?   │ └─ CreatedAt          │
│ ├─ Status       │ ├─ VoterToken    │                        │
│ ├─ ExpiresAt    │ ├─ VotedAt       │                        │
│ ├─ CreatorId    │ └─ UQ(PollCode,  │                        │
│ └─ CreatedAt    │      VoterToken) │                        │
│                 │                  │                        │
│ PollOptions     │ Questions        │                        │
│ ├─ Id (PK)      │ ├─ Id (PK)       │                        │
│ ├─ PollId (FK)  │ ├─ PollCode (ix) │                        │
│ ├─ OptionIndex  │ ├─ Text          │                        │
│ └─ Text         │ ├─ Upvotes       │                        │
│                 │ ├─ IsPinned      │                        │
│                 │ └─ CreatedAt     │                        │
└─────────────────┴──────────────────┴────────────────────────┘
```

### Entities

**Poll** (`PollDb.Polls`)

| Property | Type | Notes |
|---|---|---|
| Id | Guid | PK, `NEWID()` default |
| Code | string | 5-char shareable identifier, **unique + indexed** |
| Question | string | The poll question |
| Type | PollQuestionType enum | SingleChoice / YesNo / Rating / OpenText (string, max 20) |
| Status | PollStatus enum | Open / Closed (stored as string, max 20) |
| ExpiresAt | DateTime? | Optional expiration |
| CreatorId | Guid? | From JWT — **NOT a FK** (no Users table here) |
| CreatedAt | DateTime | UTC, `GETUTCDATE()` default |
| Options | ICollection\<PollOption\> | Navigation (1-to-many, cascade delete) |

Computed (not persisted): `IsExpired`, `IsClosed`, `IsActive`.

**PollOption** (`PollDb.PollOptions`)

| Property | Type | Notes |
|---|---|---|
| Id | Guid | PK, `NEWID()` default |
| PollId | Guid | FK → Polls (cascade delete) |
| OptionIndex | int | Display order, 0-based |
| Text | string | Option text |

**Vote** (`VoteDb.Votes`)

| Property | Type | Notes |
|---|---|---|
| Id | Guid | PK, `NEWID()` default |
| PollCode | string | Which poll — **NOT a FK** (different database) |
| OptionIndex | int | Which option was chosen (0 for OpenText) |
| TextAnswer | string? | Free-text answer for OpenText polls (max 1000); null otherwise |
| VoterToken | string | Browser fingerprint / session cookie |
| VotedAt | DateTime | UTC, `GETUTCDATE()` default |

**Question** (`VoteDb.Questions`) — anonymous Q&A

| Property | Type | Notes |
|---|---|---|
| Id | Guid | PK, `NEWID()` default |
| PollCode | string | Which poll — **NOT a FK** (different database), indexed |
| Text | string | The question text (max 1000) |
| Upvotes | int | Audience upvote count |
| IsPinned | bool | Highlighted/pinned by the host |
| CreatedAt | DateTime | UTC, `GETUTCDATE()` default |

**User** (`IdentityDb.Users`)

| Property | Type | Notes |
|---|---|---|
| Id | Guid | PK, `NEWID()` default |
| Email | string | **Unique** login |
| PasswordHash | string | BCrypt hash |
| CreatedAt | DateTime | UTC, `GETUTCDATE()` default |

### Indexes

| Database | Index | Purpose |
|---|---|---|
| PollDb | `Polls.Code` (UNIQUE) | Primary lookup by share code |
| PollDb | `Polls.CreatorId` | "My polls" query |
| PollDb | `Polls.ExpiresAt` | Cleanup service query |
| PollDb | `PollOptions.(PollId, OptionIndex)` | Ordered option lookup |
| VoteDb | `Votes.(PollCode, VoterToken)` (UNIQUE) | One vote per voter per poll |
| VoteDb | `Votes.(PollCode, OptionIndex)` | Vote-count aggregation |
| VoteDb | `Votes.VotedAt` | Analytics (votes over time) |
| VoteDb | `Questions.PollCode` | List a poll's Q&A questions |
| IdentityDb | `Users.Email` (UNIQUE) | Login lookup |

### Cross-Service References

- `Poll.CreatorId` stores a Guid taken from the JWT (via the `X-User-Id` header) — **not** a FK to `Users` (that table lives in IdentityDb).
- `Vote.PollCode` stores a string — **not** a FK to `Polls` (that table lives in PollDb).
- Cross-service validation happens via HTTP: the Vote API calls the Poll API to confirm a poll exists and is active before accepting a vote. If the Poll API is down, the Vote API rejects the vote rather than accepting a potentially invalid one.

---

## API Endpoints

All external endpoints are reached **through the Gateway**.

### Poll API

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/polls` | Optional | Create a new poll |
| GET | `/api/polls/{code}` | No | Get poll details + options |
| PATCH | `/api/polls/{code}/close` | Required (creator) | Close poll |
| DELETE | `/api/polls/{code}` | Required (creator) | Delete poll |
| GET | `/api/polls/my-polls` | Required | List creator's polls |

### Vote API

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/polls/{code}/vote` | No | Submit a vote (option index, or text for OpenText polls) |
| GET | `/api/polls/{code}/results` | No | Get vote results (or text answers for OpenText) |
| GET | `/api/polls/{code}/analytics` | No | Votes-over-time, peak minute, top option |
| GET | `/api/polls/{code}/questions` | No | List Q&A questions (pinned → upvotes → oldest) |
| POST | `/api/polls/{code}/questions` | No | Submit a Q&A question |
| POST | `/api/polls/{code}/questions/{id}/upvote` | No | Upvote a question |
| POST | `/api/polls/{code}/questions/{id}/pin` | No | Toggle a question's pinned state |
| WS | `/hubs/poll` | No | SignalR live results (`ReceiveVoteUpdate`) + Q&A (`ReceiveQuestionsUpdate`) |

### Identity API

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/register` | No | Register new user, receive JWT |
| POST | `/api/auth/login` | No | Login, receive JWT |

### Gateway Routing Table (YARP)

Routes are evaluated by `Order` (lowest first). More specific routes (vote, results, SignalR) **must** come before the catch-all poll route. Protected routes require the `authenticated` authorization policy.

A **gateway-wide YARP code transform** (`AddRequestTransform`) sets the `X-User-Id` header from the validated JWT's `sub` claim on every proxied request, and **strips any client-supplied `X-User-Id` first** (anti-spoofing). Config-based `{claim:...}` tokens are not supported by YARP, so this is done in code. On public routes the header is set only when a valid token is present (optional-auth create attribution); otherwise it is removed.

| Order | Route | Match | Cluster | Auth | X-User-Id |
|---|---|---|---|---|---|
| 1 | vote-submit | `/api/polls/{code}/vote` | vote-api | No | — |
| 2 | vote-results | `/api/polls/{code}/results` | vote-api | No | — |
| 3 | signalr-hub | `/hubs/{**remainder}` | vote-api | No | (WebSocket) |
| 4 | auth-route | `/api/auth/{**remainder}` | identity-api | No | — |
| 8 | vote-analytics | `/api/polls/{code}/analytics` | vote-api | No | — |
| 9 | vote-questions | `/api/polls/{code}/questions/{**remainder}` | vote-api | No | — |
| 5 | polls-protected | `/api/polls/my-polls` | poll-api | authenticated | ← `sub` |
| 6 | polls-close | `/api/polls/{code}/close` (PATCH) | poll-api | authenticated | ← `sub` |
| 7 | polls-delete | `/api/polls/{code}` (DELETE) | poll-api | authenticated | ← `sub` |
| 100 | polls-public | `/api/polls/{**remainder}` | poll-api | No | ← `sub` (if token present) |

Clusters: `poll-api → http://poll-api:8080`, `vote-api → http://vote-api:8080`, `identity-api → http://identity-api:8080`.

---

## Data Flows

### External request (Frontend → Gateway → Service)

All external traffic goes through the Gateway. The frontend only knows the Gateway URL (`VITE_API_URL`); it has no knowledge of individual service URLs.

```
FRONTEND (React)
  Component → Hook → axios.post('/api/polls', data)   (→ Gateway URL)
        │ HTTP (JSON)
        ▼
API GATEWAY (YARP)
  1. Match route pattern (by Order)
  2. Validate JWT if the route requires auth
  3. On success, add X-User-Id header from the JWT claim
  4. Forward to the target service; return its response
        │ HTTP (forwarded)
        ▼
TARGET MICROSERVICE
  Controller → Service → Repository → Database
  Returns response → Gateway → Frontend
```

### Service-to-service (Vote API → Poll API)

When the Vote API needs poll data it calls the Poll API **directly by Docker service name** — not through the Gateway. Internal calls skip auth validation.

```
VOTE API
  VoteService needs to know if a poll exists and is active
  → PollClientService.GetPollAsync(code)
        │ HTTP GET  http://poll-api:8080/api/polls/{code}
        ▼
POLL API
  PollsController.GetPoll(code) → PollService.GetByCodeAsync(code)
  ← 200 OK + PollResponse  (poll exists / is active)
  ← 404 Not Found          (poll doesn't exist)
```

### SignalR (real-time results)

```
1. Client opens Results Page
   → GET /api/polls/{code}/results          (initial snapshot, via Gateway)
   → Connect to /hubs/poll                  (Gateway proxies WebSocket to Vote API)
   → invoke("JoinPollGroup", pollCode)      (subscribe to this poll's group)
   → listen on "ReceiveVoteUpdate"

2. Another user votes
   → POST /api/polls/{code}/vote → Gateway → Vote API
   → VoteService saves the vote to VoteDb
   → VoteService broadcasts updated results via IHubContext to Group(code)
   → all connected clients receive "ReceiveVoteUpdate" → charts update live

3. Client leaves the page
   → invoke("LeavePollGroup", pollCode) → disconnect
```

### Authentication (cross-service)

JWT is validated **once, centrally, at the Gateway**. Downstream services trust the `X-User-Id` header the Gateway sets after validation.

```
1. POST /api/auth/register|login → Gateway → Identity API
   ← Identity API returns a JWT (7-day expiry, signed with Jwt:Secret)

2. Frontend stores it: localStorage.setItem('token', jwt)
   Axios request interceptor attaches: Authorization: Bearer <jwt>

3. POST /api/polls (protected) with token
   → Gateway validates the JWT
   → if valid: forwards request + sets X-User-Id from the JWT `sub` claim
     (YARP code transform; any client-supplied X-User-Id is stripped first)
   → if invalid/missing: returns 401 before the request reaches any service

4. Poll API reads X-User-Id (it does not re-validate the JWT)
```

`Jwt:Secret` **must be identical** in the Gateway and the Identity API (the Gateway validates tokens the Identity API signs).

---

## Environment Configuration

| Service | Variable | Dev Value | Purpose |
|---|---|---|---|
| Gateway | `Jwt__Secret` | `dev-secret-min-32-characters-here!` | JWT validation key |
| Gateway | `Frontend__Url` | `http://localhost:5173` | CORS origin |
| Poll API | `ConnectionStrings__Default` | `Server=db,1433;Database=PollDb;...` | PollDb connection |
| Vote API | `ConnectionStrings__Default` | `Server=db,1433;Database=VoteDb;...` | VoteDb connection |
| Vote API | `Services__PollApi` | `http://poll-api:8080` | Inter-service base URL |
| Vote API | `Gateway__Url` | `http://gateway:8080` | CORS origin for SignalR |
| Identity API | `ConnectionStrings__Default` | `Server=db,1433;Database=IdentityDb;...` | IdentityDb connection |
| Identity API | `Jwt__Secret` | `dev-secret-min-32-characters-here!` | JWT signing key |
| Frontend | `VITE_API_URL` | `http://localhost:5000/api` | Gateway REST URL |
| Frontend | `VITE_HUB_URL` | `http://localhost:5000/hubs/poll` | SignalR via Gateway |

> **Shared secret:** `Jwt__Secret` must be identical in the Gateway and Identity API. Dev values shown above are placeholders — production values come from the platform's secret store, never from git.

---

## Deployment Architecture

### Local development (docker-compose)

```
cp .env.example .env   # then fill in SA_PASSWORD + JWT_SECRET
docker-compose up --build
  ├─ db            SQL Server 2022      (internal only; healthcheck-gated)
  ├─ poll-api      ASP.NET 10           (internal only)
  ├─ vote-api      ASP.NET 10 + SignalR (internal only)
  ├─ identity-api  ASP.NET 10           (internal only)
  ├─ gateway       YARP                 5000 → 8080   (entry point)
  └─ frontend      Nginx                5173 → 80     (entry point)
```

**Only the Gateway (5000) and Frontend (5173) publish host ports.** The backend services and SQL Server communicate on the internal Docker network by service name; they are not reachable from the host.

**Migrations apply automatically on startup.** Each DB service calls `Database.MigrateAsync()` at boot, retrying while SQL Server initializes (the `db` healthcheck gates `depends_on`, and `EnableRetryOnFailure` covers transient faults). No manual `dotnet ef database update` step is needed — the runtime images don't include the SDK. Migration is skipped for non-relational providers (the in-memory DB used by integration tests).

Secrets (`SA_PASSWORD`, `JWT_SECRET`) come from a gitignored root `.env` via `${VAR}` interpolation; `.env.example` is the committed template.

Nginx in the frontend container proxies `/api/` and `/hubs/` to `gateway:8080` (with WebSocket upgrade headers on `/hubs/`) — it never proxies directly to individual services. The SPA is built with relative URLs (`VITE_API_URL=/api`, `VITE_HUB_URL=/hubs/poll`) so the browser calls the frontend's own origin and Nginx forwards to the Gateway.

### Production (Render)

Each service is deployed as a separate Render Web Service pulling its image from Docker Hub:

```
Render Services:
  ├─ Gateway Web Service
  ├─ Poll API Web Service
  ├─ Vote API Web Service
  ├─ Identity API Web Service
  ├─ Frontend Web Service
  └─ SQL Server Database
```

### CI/CD Pipeline (GitHub Actions — `.github/workflows/ci-cd.yml`)

```
Push to main
  │
  ├─ Phase 1: Lint & Test (all services)
  │   ├─ dotnet test services/poll-api/PollApi.sln
  │   ├─ dotnet test services/vote-api/VoteApi.sln
  │   ├─ dotnet test services/identity-api/IdentityApi.sln
  │   └─ npm ci && npm run lint   (frontend)
  │
  ├─ Phase 2: Build & Push Docker images (only on main)
  │   ├─ pollbuilder-gateway:latest
  │   ├─ pollbuilder-poll-api:latest
  │   ├─ pollbuilder-vote-api:latest
  │   ├─ pollbuilder-identity-api:latest
  │   └─ pollbuilder-frontend:latest
  │       (multi-stage builds, GHA layer cache)
  │
  └─ Phase 3: Deploy to Render (webhook per service)
      curl -X POST $RENDER_HOOK_<SERVICE>
```

Docker image naming: `pollbuilder-{service}` (e.g. `pollbuilder-poll-api`) on Docker Hub.

### Required GitHub Secrets

| Secret | Purpose |
|---|---|
| `DOCKERHUB_USERNAME` | Docker Hub login |
| `DOCKERHUB_TOKEN` | Docker Hub auth token |
| `RENDER_HOOK_GATEWAY` | Gateway deploy webhook |
| `RENDER_HOOK_POLL_API` | Poll API deploy webhook |
| `RENDER_HOOK_VOTE_API` | Vote API deploy webhook |
| `RENDER_HOOK_IDENTITY_API` | Identity API deploy webhook |
| `RENDER_HOOK_FRONTEND` | Frontend deploy webhook |

---

## Frontend Routes

| Path | Page | Purpose |
|---|---|---|
| `/` | CreatePollPage | Poll creation form |
| `/poll/:code` | VotePage | Voting page |
| `/poll/:code/results` | ResultsPage | Live results (SignalR) |
| `/my-polls` | MyPollsPage | Creator's poll dashboard |
| `/login` | LoginPage | Login |
| `/register` | RegisterPage | Registration |

---

## Design Decisions

| Decision | Rationale |
|---|---|
| **Microservices over monolith** | Independent deployment, scaling, and development per domain |
| **YARP API Gateway** | .NET-native reverse proxy with built-in transforms for JWT → `X-User-Id` |
| **Database per service** | Data ownership, no cross-service schema dependencies |
| **SignalR in Vote API only** | Only voting needs real-time; other services use plain REST |
| **`PollCode` as a string in VoteDb** | No FK across databases; validated via HTTP call to Poll API |
| **`CreatorId` as a plain Guid in PollDb** | No FK to Users (different DB); value comes from the JWT via `X-User-Id` |
| **JWT validated at Gateway only** | Centralized auth; services trust the Gateway's `X-User-Id` header |
| **`Result<T>` instead of exceptions** | Explicit control flow for expected failures across all services |
| **Typed `HttpClient` for inter-service calls** | Correct `HttpClient` lifetime; avoids socket exhaustion |
| **Docker multi-stage builds** | ~200 MB production images instead of ~900 MB |
| **Voter deduplication via token** | Session/fingerprint-based — no login required for voters |
| **`PollCleanupService` background hosted service** | Auto-closes expired polls without a manual trigger; interval is configurable (`PollCleanup:IntervalSeconds`) |
| **Question type stored as a string** (`HasConversion<string>`) | Readable in the DB; new types add safely without re-ordering an int enum |
| **OpenText answers in `Vote.TextAnswer`** | Reuses the Votes table/dedup path; results return `TextAnswers` instead of option tallies |
| **Anonymous Q&A in Vote API** | Lives next to the real-time hub; broadcasts `ReceiveQuestionsUpdate` like vote updates — no login required |
