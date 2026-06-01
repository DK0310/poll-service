---
name: pollbuilder-architecture
description: Use when designing a new feature, understanding the system structure, or deciding where code belongs in the Poll & Survey Builder microservice architecture
---

# Poll Builder — Architecture Skill

## System Overview

Poll Builder is a **microservices-based** poll and survey creation platform. Creators build multiple-choice polls, share a short link, and collect votes from respondents in real time. Results update live via SignalR WebSockets — no page refresh required.

**Architecture style:** Microservices with an API Gateway. Each service owns its domain, its data, and its deployment lifecycle. Services communicate via synchronous HTTP calls when needed, and are independently deployable.

---

## Tech Stack

| Layer | Technology | Version | Purpose |
|---|---|---|---|
| Frontend | React + TypeScript + Vite | React 18, Vite 5 | SPA user interface |
| API Gateway | ASP.NET Core + YARP | .NET 8 | Reverse proxy, request routing |
| Poll Service | ASP.NET Core Web API | .NET 8 | Poll CRUD operations |
| Vote Service | ASP.NET Core Web API + SignalR | .NET 8 | Voting, results, real-time broadcast |
| Identity Service | ASP.NET Core Web API | .NET 8 | Auth, JWT token management |
| ORM | Entity Framework Core | EF Core 8 | Database abstraction (per service) |
| Database | SQL Server | 2022 | Each service gets its own database |
| Charts | Chart.js / Recharts | — | Live animated bar charts on results page |
| Auth | JWT Bearer tokens | — | 7-day expiration, validated at Gateway |
| Containers | Docker multi-stage | — | One Dockerfile per service |
| Orchestration | docker-compose | — | Local multi-service development |
| CI/CD | GitHub Actions | — | Lint → Test → Build → Push → Deploy |
| Registry | Docker Hub | — | Image storage |
| Hosting | Render | — | Production deployment |

---

## Microservice Architecture Diagram

```
                           ┌─────────────────┐
                           │    Frontend      │
                           │  (React SPA)     │
                           │  Port 5173       │
                           └────────┬─────────┘
                                    │ HTTP / WebSocket
                           ┌────────▼─────────┐
                           │   API Gateway     │
                           │   (YARP)          │
                           │   Port 5000       │
                           └──┬─────┬──────┬──┘
              ┌───────────────┤     │      ├───────────────┐
              │               │     │      │               │
     ┌────────▼────────┐     │     │      │    ┌──────────▼──────────┐
     │  Identity API    │     │     │      │    │    Vote API          │
     │  /api/auth/*     │     │     │      │    │    /api/polls/*/vote │
     │  Port 5003       │     │     │      │    │    /api/polls/*/results│
     │                  │     │     │      │    │    /hubs/poll (SignalR)│
     └────────┬─────────┘     │     │      │    │    Port 5002         │
              │               │     │      │    └──────────┬───────────┘
     ┌────────▼────────┐      │     │      │    ┌──────────▼───────────┐
     │  IdentityDb      │     │     │      │    │    VoteDb             │
     └─────────────────┘      │     │      │    └──────────────────────┘
                              │     │      │
                     ┌────────▼─────▼──────▼──┐
                     │     Poll API            │
                     │     /api/polls/*        │
                     │     Port 5001           │
                     └────────┬───────────────┘
                     ┌────────▼───────────────┐
                     │     PollDb              │
                     └────────────────────────┘
```

---

## Service Responsibilities

### The Golden Rule

> **Each service owns its data and its domain. No service directly accesses another service's database.**

| Service | Owns | Exposes | Consumes |
|---|---|---|---|
| **Poll API** | Polls, PollOptions | Poll CRUD endpoints | Nothing — it's self-contained |
| **Vote API** | Votes | Vote submission, results, SignalR Hub | Calls Poll API to validate poll exists and is active |
| **Identity API** | Users | Register, login, JWT generation | Nothing — it's self-contained |
| **API Gateway** | Nothing (stateless) | Unified `/api/*` routes | Routes to all services, validates JWT |

### Inter-Service Communication

```
Vote API needs to check if a poll exists and is active before accepting a vote:

  Vote API  ──HTTP GET──▶  Poll API /api/polls/{code}
                           ◀── 200 OK + PollResponse (poll exists, is active)
                           ◀── 404 Not Found (poll doesn't exist)

This is SYNCHRONOUS communication via HttpClient.
The Gateway is NOT involved in service-to-service calls.
Services call each other directly using Docker service names.
```

---

## Folder Structure

```
/
├── services/
│   ├── poll-api/                          ← Poll management microservice
│   │   ├── PollApi/                       ← ASP.NET Core Web API
│   │   │   ├── Controllers/
│   │   │   │   └── PollsController.cs     ← Poll CRUD
│   │   │   ├── Services/
│   │   │   │   └── PollService.cs         ← Business logic
│   │   │   ├── Repositories/
│   │   │   │   └── PollRepository.cs      ← Data access
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
│   │   │   └── Program.cs
│   │   ├── PollApi.Tests/                 ← Unit tests
│   │   ├── Dockerfile
│   │   └── PollApi.sln
│   │
│   ├── vote-api/                          ← Voting + real-time microservice
│   │   ├── VoteApi/                       ← ASP.NET Core Web API + SignalR
│   │   │   ├── Controllers/
│   │   │   │   └── VotesController.cs     ← Vote submission + results
│   │   │   ├── Hubs/
│   │   │   │   └── PollHub.cs             ← SignalR for live results
│   │   │   ├── Services/
│   │   │   │   ├── VoteService.cs         ← Vote logic + broadcast
│   │   │   │   └── PollClientService.cs   ← HTTP client to Poll API
│   │   │   ├── Repositories/
│   │   │   │   └── VoteRepository.cs
│   │   │   ├── DTOs/
│   │   │   │   ├── VoteRequest.cs
│   │   │   │   └── VoteResultsResponse.cs
│   │   │   ├── Models/
│   │   │   │   └── Vote.cs
│   │   │   ├── Data/
│   │   │   │   ├── VoteDbContext.cs
│   │   │   │   └── Migrations/
│   │   │   ├── Middleware/
│   │   │   │   └── ErrorHandlingMiddleware.cs
│   │   │   └── Program.cs
│   │   ├── VoteApi.Tests/
│   │   ├── Dockerfile
│   │   └── VoteApi.sln
│   │
│   ├── identity-api/                      ← Auth microservice
│   │   ├── IdentityApi/                   ← ASP.NET Core Web API
│   │   │   ├── Controllers/
│   │   │   │   └── AuthController.cs      ← Register/login
│   │   │   ├── Services/
│   │   │   │   └── AuthService.cs         ← JWT generation
│   │   │   ├── Models/
│   │   │   │   └── User.cs
│   │   │   ├── Data/
│   │   │   │   ├── IdentityDbContext.cs
│   │   │   │   └── Migrations/
│   │   │   └── Program.cs
│   │   ├── IdentityApi.Tests/
│   │   ├── Dockerfile
│   │   └── IdentityApi.sln
│   │
│   └── gateway/                           ← API Gateway (YARP)
│       ├── Gateway/
│       │   ├── Program.cs                 ← YARP config, JWT validation
│       │   └── appsettings.json           ← Route definitions
│       ├── Dockerfile
│       └── Gateway.sln
│
├── frontend/
│   ├── src/
│   │   ├── api/                           ← api.ts (axios → gateway)
│   │   ├── types/                         ← poll.types.ts
│   │   ├── hooks/                         ← useCreatePoll, useVote, useLiveResults
│   │   ├── components/                    ← PollForm, VoteForm, LiveBarChart
│   │   ├── pages/                         ← CreatePollPage, VotePage, ResultsPage
│   │   └── App.tsx
│   ├── vite.config.ts
│   ├── Dockerfile
│   └── .env                               ← VITE_API_URL (points to gateway)
│
├── .github/workflows/
│   └── ci-cd.yml                          ← Builds + deploys ALL services
├── docker-compose.yml                     ← Local orchestration (all services)
├── ARCHITECTURE.md                        ← System documentation
└── README.md
```

---

## Naming Conventions

| What | Pattern | Example | Location |
|---|---|---|---|
| Microservice folder | `{name}-api` / `{name}` | `poll-api`, `gateway` | `services/` |
| Controller | `{Resource}Controller` | `PollsController` | `{Service}/Controllers/` |
| Service class | `{Resource}Service` | `PollService` | `{Service}/Services/` |
| Inter-service client | `{Remote}ClientService` | `PollClientService` | `{Service}/Services/` |
| Repository class | `{Resource}Repository` | `PollRepository` | `{Service}/Repositories/` |
| DbContext | `{Service}DbContext` | `PollDbContext` | `{Service}/Data/` |
| Request DTO | `{Action}{Resource}Request` | `CreatePollRequest` | `{Service}/DTOs/` |
| Response DTO | `{Resource}Response` | `PollResponse` | `{Service}/DTOs/` |
| Entity/Model | PascalCase noun | `Poll`, `Vote`, `User` | `{Service}/Models/` |
| SignalR Hub | `{Resource}Hub` | `PollHub` | `VoteApi/Hubs/` |
| Docker image | `pollbuilder-{service}` | `pollbuilder-poll-api` | Docker Hub |
| React page | `{Purpose}Page` | `CreatePollPage` | `frontend/src/pages/` |
| React hook | `use{What}` | `useLiveResults` | `frontend/src/hooks/` |
| API route | lowercase plural | `/api/polls` | Controller attribute |

---

## Request Flow — Through the Gateway

Every external request flows through the API Gateway:

```
┌─────────────────────────────────────────────────────────────────┐
│  FRONTEND (React)                                               │
│  Component → Hook → axios.post('/api/polls', pollData)          │
│  All requests go to the Gateway URL (VITE_API_URL)              │
└─────────────────────────┬───────────────────────────────────────┘
                          │ HTTP (JSON)
┌─────────────────────────▼───────────────────────────────────────┐
│  API GATEWAY (YARP)                                              │
│  1. Match route pattern                                          │
│  2. Validate JWT (if route requires auth)                        │
│  3. Forward request to target microservice                       │
│  4. Return response to client                                    │
│                                                                  │
│  Route table:                                                    │
│  /api/polls (CRUD)         →  http://poll-api:8080               │
│  /api/polls/*/vote         →  http://vote-api:8080               │
│  /api/polls/*/results      →  http://vote-api:8080               │
│  /api/auth/*               →  http://identity-api:8080           │
│  /hubs/poll                →  http://vote-api:8080 (WebSocket)   │
└─────────────────────────┬───────────────────────────────────────┘
                          │ HTTP (forwarded)
┌─────────────────────────▼───────────────────────────────────────┐
│  TARGET MICROSERVICE                                             │
│  Controller → Service → Repository → Database                    │
│  Returns response → Gateway → Frontend                           │
└─────────────────────────────────────────────────────────────────┘
```

## Request Flow — Service-to-Service

When the Vote API needs poll data, it calls the Poll API directly (NOT through the Gateway):

```
┌─────────────────────────────────────────────────────────────────┐
│  VOTE API                                                        │
│  VoteService needs to check if poll exists and is active         │
│  → calls PollClientService.GetPollAsync(code)                    │
└─────────────────────────┬───────────────────────────────────────┘
                          │ HTTP (direct, internal)
                          │ http://poll-api:8080/api/polls/{code}
┌─────────────────────────▼───────────────────────────────────────┐
│  POLL API                                                        │
│  PollsController.GetPoll(code)                                   │
│  → PollService.GetByCodeAsync(code)                              │
│  → Returns PollResponse                                          │
└─────────────────────────────────────────────────────────────────┘

**Rule:** Service-to-service calls use Docker service names
         (e.g., http://poll-api:8080), not localhost or the gateway.
```

## Request Flow — SignalR (Real-Time)

```
┌───────────────────────────────────────────────────────────────────┐
│  FRONTEND (React — Results Page)                                   │
│  1. Connect to Gateway /hubs/poll                                  │
│  2. Gateway proxies WebSocket to Vote API                          │
│  3. Client calls hub.invoke("JoinPollGroup", pollCode)             │
│  4. Listens: connection.on("ReceiveVoteUpdate", updateChart)       │
└────────────────────┬──────────────────────────────────────────────┘
                     │ WebSocket (via Gateway proxy)
┌────────────────────▼──────────────────────────────────────────────┐
│  VOTE API — SIGNALR HUB (PollHub)                                  │
│  - JoinPollGroup(code) → Groups.AddToGroupAsync(code)              │
│  - On vote: VoteService broadcasts via IHubContext to group        │
└───────────────────────────────────────────────────────────────────┘
```

---

## Where Does This Logic Go?

### Which SERVICE does this code belong in?

```
IS IT ABOUT...

├─ Creating, reading, updating, deleting a POLL?
│  └─ POLL API
│     PollsController → PollService → PollRepository → PollDb
│
├─ Submitting a VOTE?
│  └─ VOTE API
│     VotesController → VoteService → VoteRepository → VoteDb
│     (VoteService calls PollClientService to verify poll exists)
│
├─ Getting vote RESULTS or live updates?
│  └─ VOTE API
│     VotesController → VoteService → VoteRepository
│     PollHub (SignalR) for real-time broadcasting
│
├─ User REGISTRATION or LOGIN?
│  └─ IDENTITY API
│     AuthController → AuthService → IdentityDb
│
├─ ROUTING a request to the correct service?
│  └─ API GATEWAY
│     YARP route configuration in appsettings.json
│
├─ Validating a JWT TOKEN?
│  └─ API GATEWAY (centralized)
│     Each service trusts the Gateway has validated the token
│     Services can also validate locally if needed
│
└─ Rendering UI or calling an API?
   └─ FRONTEND
      Pages, components, hooks — all calls go to the Gateway URL
```

### Within a service, which LAYER does this code belong in?

```
IS IT...

├─ Accepting/parsing an HTTP request?
│  └─ CONTROLLER
│
├─ Returning an HTTP status code?
│  └─ CONTROLLER
│
├─ Validating a business rule?
│  └─ SERVICE
│
├─ Calling another microservice?
│  └─ CLIENT SERVICE (e.g., PollClientService)
│     Uses HttpClient to call the other service's API
│
├─ Broadcasting real-time updates?
│  └─ SERVICE (via IHubContext<PollHub>)
│
├─ Reading/writing to the database?
│  └─ REPOSITORY
│
├─ Defining data shape for the API?
│  └─ DTO (DTOs/)
│
└─ Defining persistent data shape?
   └─ MODEL (Models/)
```

---

## Design Patterns

### 1. Result\<T\> — No Exceptions for Control Flow

Used in ALL services. Services NEVER throw exceptions for expected failures.

```csharp
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    private Result(T value) { IsSuccess = true; Value = value; }
    private Result(string error) { IsSuccess = false; Error = error; }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error) => new(error);
}
```

**Each service has its own copy** of the `Result<T>` class, or it's in a shared NuGet package / shared project.

### 2. Inter-Service Client — HttpClient Wrapper

```csharp
// In Vote API — Services/PollClientService.cs
public class PollClientService
{
    private readonly HttpClient _http;

    public PollClientService(HttpClient http)
    {
        _http = http;
    }

    public async Task<PollInfo?> GetPollAsync(string code)
    {
        try
        {
            var response = await _http.GetAsync($"/api/polls/{code}");
            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<PollInfo>();
        }
        catch (HttpRequestException)
        {
            return null; // Poll API unavailable
        }
    }
}

// Registered in Vote API's Program.cs:
builder.Services.AddHttpClient<PollClientService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:PollApi"]!);
    // e.g., "http://poll-api:8080"
});
```

**Rule:** Always use `AddHttpClient<T>()` for typed HTTP clients. This manages `HttpClient` lifetime correctly and avoids socket exhaustion.

### 3. API Gateway — YARP Route Configuration

```json
// gateway/Gateway/appsettings.json
{
  "ReverseProxy": {
    "Routes": {
      "poll-route": {
        "ClusterId": "poll-api",
        "Match": { "Path": "/api/polls/{**catch-all}" }
      },
      "vote-route": {
        "ClusterId": "vote-api",
        "Match": { "Path": "/api/polls/{code}/vote" }
      },
      "results-route": {
        "ClusterId": "vote-api",
        "Match": { "Path": "/api/polls/{code}/results" }
      },
      "auth-route": {
        "ClusterId": "identity-api",
        "Match": { "Path": "/api/auth/{**catch-all}" }
      },
      "signalr-route": {
        "ClusterId": "vote-api",
        "Match": { "Path": "/hubs/{**catch-all}" }
      }
    },
    "Clusters": {
      "poll-api": {
        "Destinations": {
          "default": { "Address": "http://poll-api:8080" }
        }
      },
      "vote-api": {
        "Destinations": {
          "default": { "Address": "http://vote-api:8080" }
        }
      },
      "identity-api": {
        "Destinations": {
          "default": { "Address": "http://identity-api:8080" }
        }
      }
    }
  }
}
```

**Route priority matters:** More specific routes (vote, results) must be listed before the catch-all poll route. YARP evaluates routes by specificity.

### 4. Database Per Service

```
Poll API    → PollDb    (Polls, PollOptions tables)
Vote API    → VoteDb    (Votes table)
Identity API → IdentityDb (Users table)
```

**Rule:** A service NEVER directly queries another service's database. It calls the other service's API instead.

### 5. SignalR Hub — Real-Time Vote Broadcasting

```csharp
// In Vote API — Hubs/PollHub.cs
public class PollHub : Hub
{
    public async Task JoinPollGroup(string pollCode)
        => await Groups.AddToGroupAsync(Context.ConnectionId, pollCode);

    public async Task LeavePollGroup(string pollCode)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, pollCode);
}

// VoteService broadcasts via IHubContext (not through the Hub class):
await _hubContext.Clients.Group(code)
    .SendAsync("ReceiveVoteUpdate", results);
```

---

## API Gateway Configuration

**Location:** `services/gateway/Gateway/Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

// Load YARP configuration from appsettings.json
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// JWT validation at Gateway level (centralized)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

// CORS — frontend talks to gateway only
builder.Services.AddCors(opt => opt.AddPolicy("Frontend", p =>
    p.WithOrigins(builder.Configuration["Frontend:Url"]!)
     .AllowAnyMethod().AllowAnyHeader().AllowCredentials()));

var app = builder.Build();

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();

app.Run();
```

---

## Authentication Flow (Across Services)

```
1. POST /api/auth/register → Gateway → Identity API
   ← Identity API returns JWT token

2. Frontend stores token: localStorage.setItem('token', jwt)

3. POST /api/polls (create poll, with token)
   → Gateway validates JWT (centralized auth)
   → Forwards to Poll API with user ID in header
   → Poll API reads X-User-Id header set by Gateway

4. Protected endpoints: Gateway enforces auth
   → If valid: forwards request + adds X-User-Id header
   → If invalid: returns 401 before request reaches service
```

### JWT Shared Secret

All services that need to validate JWT share the same `Jwt:Secret` configuration. The Gateway does primary validation; downstream services can optionally re-validate.

---

## Entity Overview (per service)

### Poll API Entities

**Poll:**

| Property | Type | Purpose |
|---|---|---|
| Id | Guid | Primary key |
| Code | string | 5-char shareable identifier (unique, indexed) |
| Question | string | The poll question |
| Status | PollStatus | Open / Closed (enum) |
| ExpiresAt | DateTime? | Optional expiration |
| CreatorId | Guid? | User who created it (from JWT, not FK — no Users table here) |
| CreatedAt | DateTime | Creation timestamp (UTC) |
| Options | ICollection\<PollOption\> | Navigation (1-to-many) |

**PollOption:**

| Property | Type | Purpose |
|---|---|---|
| Id | Guid | Primary key |
| PollId | Guid | FK to Polls |
| OptionIndex | int | Display order (0-based) |
| Text | string | Option text |

### Vote API Entities

**Vote:**

| Property | Type | Purpose |
|---|---|---|
| Id | Guid | Primary key |
| PollCode | string | Which poll (indexed — no FK, different DB) |
| OptionIndex | int | Which option was chosen |
| VoterToken | string | Browser fingerprint / session cookie |
| VotedAt | DateTime | Vote timestamp (UTC) |

> **Note:** Vote stores `PollCode` (string), not a FK to Polls. The Vote API's database has no Polls table — it validates polls by calling the Poll API over HTTP.

### Identity API Entities

**User:**

| Property | Type | Purpose |
|---|---|---|
| Id | Guid | Primary key |
| Email | string | Unique login |
| PasswordHash | string | BCrypt hash |
| CreatedAt | DateTime | Account creation (UTC) |

---

## Environment Configuration

### Service URLs (docker-compose internal)

| Service | Internal URL | External Port |
|---|---|---|
| Poll API | `http://poll-api:8080` | 5001 |
| Vote API | `http://vote-api:8080` | 5002 |
| Identity API | `http://identity-api:8080` | 5003 |
| Gateway | `http://gateway:8080` | 5000 |
| Frontend | `http://frontend:80` | 5173 |
| SQL Server | `db:1433` | 1433 |

### Shared Configuration

| Variable | Purpose | Used By |
|---|---|---|
| `Jwt__Secret` | Token signing key (must be identical) | Gateway, Identity API |
| `ConnectionStrings__Default` | Per-service database | Each API service |
| `Services__PollApi` | Poll API base URL | Vote API |
| `Frontend__Url` | CORS origin | Gateway |
| `VITE_API_URL` | Gateway URL | Frontend |
| `VITE_HUB_URL` | SignalR hub URL (via gateway) | Frontend |

---

## End-to-End: Adding a New Feature

Example: Adding "poll analytics" as a new microservice feature.

### Decision: Where does it go?

```
Analytics is about aggregating vote data → VOTE API
(It reads from votes, which Vote API owns)

But if analytics grows complex, it could become its own service:
ANALYTICS API → reads from VoteDb (or gets data from Vote API)
```

### For now: Add to Vote API

1. Create `PollAnalyticsResponse` DTO
2. Add `GetAnalyticsAsync()` to `VoteService`
3. Add `GET /api/polls/{code}/analytics` to `VotesController`
4. Add route in Gateway: `/api/polls/{code}/analytics → vote-api`
5. Add `useAnalytics` hook in frontend
6. Write tests

### If it becomes its own service later:

1. Create `services/analytics-api/` with its own solution
2. Give it read access to VoteDb (or call Vote API for data)
3. Add Dockerfile, docker-compose service, Gateway route
4. Deploy independently

---

## Common Mistakes

| ❌ Don't | ✅ Do | Why |
|---|---|---|
| Share a database between services | Each service has its own database | Data ownership, independent deployment |
| Call another service's DB directly | Use HTTP client to call its API | Microservice boundary enforcement |
| Route service-to-service through Gateway | Call directly: `http://poll-api:8080` | Gateway is for external traffic only |
| Put JWT validation in every service | Validate at Gateway (centralized) | Single point of auth configuration |
| Create one massive Dockerfile | One Dockerfile per service | Independent builds and deploys |
| Deploy all services together | Deploy independently per service | That's the whole point of microservices |
| Use `new HttpClient()` in services | Use `AddHttpClient<T>()` in DI | Prevents socket exhaustion |
| Skip health checks | Add `/health` endpoint per service | Monitoring and orchestration |
| Hard-code service URLs | Use configuration/env vars | Different per environment |
| Forget WebSocket headers in Gateway/Nginx | Include Upgrade + Connection headers | SignalR requires WebSocket proxying |

---

## Cross-References

- **Implementing service endpoints** → `pollbuilder-backend/SKILL.md`
- **Database per service** → `pollbuilder-database/SKILL.md`
- **Building React components** → `pollbuilder-frontend/SKILL.md`
- **Writing tests** → `pollbuilder-testing/SKILL.md`
- **Docker, CI/CD, deployment** → `pollbuilder-devops/SKILL.md`
