# Architecture Diagrams (Mermaid)

Mermaid versions of the system described in [ARCHITECTURE.md](../ARCHITECTURE.md) (the authoritative
source — if these ever disagree, that file wins). GitHub and VS Code (with Mermaid support) render
these natively; they're also handy for slides.

---

## 1. System topology (services, databases, real-time)

```mermaid
flowchart TB
    B["🌐 Browser"]

    subgraph FE["Frontend — React 19 + Vite SPA :5173"]
        SPA["Pages + Hooks<br/>axios → VITE_API_URL<br/>@microsoft/signalr → VITE_HUB_URL"]
    end

    subgraph GW["API Gateway — YARP :5000"]
        direction TB
        JWTV["JWT validation (Jwt:Secret)<br/>policies: authenticated / admin"]
        XFRM["Code transform:<br/>strip + set X-User-Id / X-User-Role"]
        JWTV --- XFRM
    end

    subgraph PS["Poll API :5001"]
        PC["PollsController · AdminPollsController"]
        PCS["PollCleanupService<br/>(background expiry sweep)"]
    end

    subgraph VS["Vote API :5002"]
        VC["VotesController · QuestionsController"]
        HUB["PollHub — SignalR<br/>/hubs/poll"]
    end

    subgraph IS["Identity API :5003"]
        AC["AuthController · AdminUsersController"]
    end

    PDB[("PollDb<br/>Polls · PollOptions")]
    VDB[("VoteDb<br/>Votes · Questions · QuestionUpvotes")]
    IDB[("IdentityDb<br/>Users")]

    B --> SPA
    SPA -->|"HTTP /api/*"| GW
    SPA <-->|"WebSocket /hubs/poll"| GW
    GW -->|"/api/polls/* · /api/admin/polls/*"| PC
    GW -->|"vote · results · analytics · questions"| VC
    GW <-->|"WS proxy"| HUB
    GW -->|"/api/auth/* · /api/admin/users/*"| AC
    VC -->|"GET /api/polls/:code<br/>validate poll (typed HttpClient)"| PC
    PC --> PDB
    PCS --> PDB
    VC --> VDB
    HUB --> VDB
    AC --> IDB
```

**Golden rule shown above:** each service touches only its own DB; the Vote API reaches poll data
via HTTP to the Poll API, never via PollDb.

---

## 2. Gateway routing (YARP route → cluster, with auth policy)

```mermaid
flowchart LR
    REQ["Incoming request"] --> M{"YARP match<br/>lowest Order wins"}

    subgraph VAPI["→ vote-api"]
        R1["1 · POST /api/polls/:code/vote — public"]
        R2["2 · GET /api/polls/:code/results — public"]
        R3["3 · /hubs/** — public (WebSocket)"]
        R8["8 · GET /api/polls/:code/analytics — 🔒 authenticated"]
        R9["9 · /api/polls/:code/questions/** — public"]
    end

    subgraph PAPI["→ poll-api"]
        R5["5 · GET /api/polls/my-polls — 🔒 authenticated"]
        R6["6 · PATCH /api/polls/:code/close — 🔒 authenticated"]
        R7["7 · DELETE /api/polls/:code — 🔒 authenticated"]
        R10["10 · POST /api/polls — 🔒 authenticated"]
        R11["11 · /api/admin/polls/** — 🔑 admin"]
        R100["100 · /api/polls/** — public (catch-all)"]
    end

    subgraph IAPI["→ identity-api"]
        R4["4 · /api/auth/** — public"]
        R12["12 · /api/admin/users/** — 🔑 admin"]
    end

    M --> VAPI
    M --> PAPI
    M --> IAPI
```

Every proxied request passes the **anti-spoof transform**: client-supplied `X-User-Id`/`X-User-Role`
are stripped, then re-set from the validated JWT's `sub`/`role` claims when a token is present.

---

## 3. Flow — vote submission + live results (SignalR)

```mermaid
sequenceDiagram
    autonumber
    actor V as Voter (browser A)
    actor W as Watcher (browser B)
    participant GW as Gateway (YARP)
    participant VA as Vote API
    participant PA as Poll API
    participant DB as VoteDb

    Note over W,VA: Watcher opens the Results page
    W->>GW: GET /api/polls/:code/results
    GW->>VA: forward
    VA-->>W: initial snapshot
    W->>GW: connect WebSocket /hubs/poll
    GW->>VA: proxy WS (upgrade)
    W->>VA: invoke JoinPollGroup(code)

    Note over V,DB: Voter submits a vote
    V->>GW: POST /api/polls/:code/vote { optionIndex, voterToken }
    GW->>VA: forward
    VA->>PA: GET /api/polls/:code — exists + active?
    PA-->>VA: 200 PollResponse (incl. creatorId)
    VA->>DB: dedup unique(PollCode, VoterToken) → save Vote
    VA-->>V: 200 updated results (409 if duplicate)
    VA--)W: ReceiveVoteUpdate → chart animates live
```

---

## 4. Flow — authentication + protected request (RBAC)

```mermaid
sequenceDiagram
    autonumber
    actor U as User
    participant FE as SPA
    participant GW as Gateway
    participant IA as Identity API
    participant PA as Poll API

    U->>FE: log in (email, password)
    FE->>GW: POST /api/auth/login
    GW->>IA: forward (public route)
    IA->>IA: BCrypt verify → sign JWT (sub, email, role, jti · 7-day)
    IA-->>FE: { token }
    FE->>FE: localStorage['token'] + decode role (UX only)

    U->>FE: create a poll
    FE->>GW: POST /api/polls — Authorization: Bearer
    GW->>GW: validate JWT + policy "authenticated"
    GW->>GW: strip client X-User-* → set from claims
    GW->>PA: forward + X-User-Id + X-User-Role
    PA->>PA: CreatorId = X-User-Id (no JWT re-validation)
    PA-->>FE: 201 PollResponse
    Note over GW: missing/invalid token → 401 before any service<br/>admin route without role=Admin → 403
    Note over PA: fine-grained: owner = X-User-Id == CreatorId<br/>admin = X-User-Role == Admin (re-checked in admin controllers)
```

---

## 5. Database schema (3 databases — dotted lines = logical refs, **no FK**)

```mermaid
erDiagram
    POLL ||--o{ POLL_OPTION : "has (FK, cascade delete)"
    POLL ||..o{ VOTE : "by Code (cross-DB, no FK)"
    POLL ||..o{ QUESTION : "by PollCode (cross-DB, no FK)"
    QUESTION ||..o{ QUESTION_UPVOTE : "by QuestionId (flat, no nav)"
    USER ||..o{ POLL : "CreatorId via JWT (cross-DB, no FK)"

    POLL {
        guid Id PK
        string Code UK "5-char share code"
        string Question
        string Type "SingleChoice-YesNo-Rating-OpenText"
        string Status "Open-Closed"
        datetime ExpiresAt "nullable"
        guid CreatorId "nullable, indexed"
        datetime CreatedAt
    }
    POLL_OPTION {
        guid Id PK
        guid PollId FK
        int OptionIndex
        string Text
    }
    VOTE {
        guid Id PK
        string PollCode "UQ with VoterToken"
        int OptionIndex
        string TextAnswer "nullable, OpenText only"
        string VoterToken "browser token"
        datetime VotedAt "indexed (analytics)"
    }
    QUESTION {
        guid Id PK
        string PollCode "indexed"
        string Text
        int Upvotes "distinct upvoters"
        bool IsPinned "owner-admin only"
        datetime CreatedAt
    }
    QUESTION_UPVOTE {
        guid Id PK
        guid QuestionId "UQ with VoterKey"
        string VoterKey "userId or voterToken"
        datetime CreatedAt
    }
    USER {
        guid Id PK
        string Email UK
        string PasswordHash "BCrypt"
        string Role "User-Admin, default User"
        datetime CreatedAt
    }
```

DB ownership: `POLL`/`POLL_OPTION` → **PollDb**; `VOTE`/`QUESTION`/`QUESTION_UPVOTE` → **VoteDb**;
`USER` → **IdentityDb**. Dedup is enforced by unique indexes `(PollCode, VoterToken)` and
`(QuestionId, VoterKey)`.

---

## 6. Per-service internal layering (the request pipeline inside each service)

```mermaid
flowchart LR
    HTTP["HTTP request<br/>(from Gateway)"] --> MW["ErrorHandlingMiddleware<br/>(unhandled → JSON 500)"]
    MW --> C["Controller<br/>routes · status codes ·<br/>reads X-User-Id / X-User-Role"]
    C --> S["Service<br/>business rules · validation ·<br/>returns Result&lt;T&gt;"]
    S --> R["Repository<br/>EF queries only ·<br/>virtual for Moq"]
    R --> D["DbContext<br/>mapping · indexes · migrations"]
    D --> SQL[("SQL Server")]

    S -. "Vote API only" .-> H["IHubContext&lt;PollHub&gt;<br/>group broadcast"]
    S -. "Vote API only" .-> PC["PollClientService<br/>HTTP → Poll API"]
```

Exceptions: **Identity API** has no Repository layer (`AuthService`/`AdminService` use the DbContext
directly); the **Gateway** has none of these layers (YARP config only).

---

## 7. RBAC — layered enforcement (defense-in-depth)

```mermaid
flowchart TB
    A["Action from browser<br/>(Guest · User · Admin)"] --> F["Layer 3 — Frontend UX<br/>RequireAuth / RequireAdmin guards ·<br/>role-gated UI (Create, analytics, Pin, Admin nav)"]
    F --> G{"Layer 1 — Gateway policy"}
    G -->|"public route"| H["forward (+X-User-* if token)"]
    G -->|"authenticated · no/invalid JWT"| E401["401"]
    G -->|"admin · role ≠ Admin"| E403g["403"]
    G -->|"policy passes"| H
    H --> S{"Layer 2 — Service check"}
    S -->|"owner: X-User-Id == CreatorId"| OK["200 ✅"]
    S -->|"X-User-Role == Admin (re-checked)"| OK
    S -->|"neither"| E403s["403"]
    S -->|"duplicate vote/upvote"| E409["409"]
```

The server is always authoritative — the frontend layer is UX-only.

---

## 8. CI/CD + production deployment (Render)

```mermaid
flowchart LR
    DEV["git push → main"] --> J1["Job 1 — Lint & Test<br/>dotnet test ×3 ·<br/>npm lint + build"]
    J1 --> J2["Job 2 — Build & push<br/>5 Docker images → Docker Hub<br/>(multi-stage, GHA cache)"]
    J2 --> J3["Job 3 — Deploy<br/>Render webhooks (env-guarded)"]

    subgraph PROD["Render production"]
        direction TB
        SS["Frontend — Static Site<br/>CDN · no cold start ·<br/>VITE_* baked to gateway URL"]
        GWW["Gateway — Web Service"]
        PW["Poll API — Web Service"]
        VW["Vote API — Web Service"]
        IW["Identity API — Web Service"]
        SQLP[("SQL Server")]
    end

    J3 --> GWW & PW & VW & IW
    DEV -.->|"auto-build from git"| SS
    SS -->|"cross-origin HTTPS + WS<br/>(CORS: Frontend__Url = site origin)"| GWW
    GWW --> PW & VW & IW
    PW & VW & IW --> SQLP

    note["⚠ free tier: backends sleep when idle →<br/>SPA fires warm-up pings on load (api/warmup.ts)"]
    SS -.- note
```

Migrations auto-apply on each service's startup (`Database.MigrateAsync()` with retry) — no manual
`dotnet ef database update` in any environment.
