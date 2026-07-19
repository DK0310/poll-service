---
name: pollbuilder-architecture
description: Use when designing a new feature, understanding the system structure, or deciding where code belongs in the Poll & Survey Builder microservice architecture
---

# Poll Builder — Architecture Skill

This skill holds the **reusable** architectural thinking for this project: principles, decision trees, naming conventions, review checklists, and design heuristics.

> **Project facts live in [ARCHITECTURE.md](../../../ARCHITECTURE.md)** — the authoritative source for the service map, ports, folder/file structure, schema, endpoints, data-flow diagrams, environment variables, and deployment topology. This skill does not repeat them; consult it whenever you need a concrete name, path, port, or column.

---

## Core Principles

1. **The Golden Rule — own your data.** Each service owns its domain and its database. No service ever reads or writes another service's database. Cross-service data is fetched via an HTTP API call, never a shared connection string or a FK across databases.
2. **One gateway, one front door.** All *external* traffic enters through the API Gateway. The frontend knows only the Gateway URL. Internal service-to-service calls bypass the Gateway and use Docker service names directly.
3. **Centralize auth at the edge.** Validate the JWT once, at the Gateway. Downstream services trust a header the Gateway sets (here, `X-User-Id`) rather than re-validating tokens everywhere.
4. **Independently deployable.** One Dockerfile, one test project, one migration history per service. A change in one service must not force a redeploy of another.
5. **Synchronous only when necessary.** Reach for an inter-service HTTP call only when a service genuinely needs another's data to complete a request. Prefer owning the data you need.
6. **Real-time is a capability, not a default.** Add SignalR/WebSockets only to the service that needs to push (here, voting). Everything else stays plain REST.

---

## Decision Tree — Which SERVICE owns this code?

Map the responsibility to its owning domain. (For the concrete service/port/route map, see [ARCHITECTURE.md](../../../ARCHITECTURE.md).)

```
WHAT IS THIS ABOUT?

├─ The lifecycle of the core resource (create / read / update / delete / list)?
│  └─ The service that OWNS that resource and its table.

├─ An action against a resource owned by another service
│  (e.g. recording something that references it)?
│  └─ The service that OWNS the new data.
│     It calls the owning service over HTTP to validate the reference first.

├─ A derived/aggregate view of data (results, counts, analytics)?
│  └─ The service that OWNS the underlying rows.
│     If it grows into its own bounded domain, split it into a new service.

├─ Authentication / identity / token issuing?
│  └─ The identity service. Nothing else issues or stores credentials.

├─ Routing, JWT validation, or header enrichment for external traffic?
│  └─ The API Gateway (stateless).

└─ Rendering UI or calling an API?
   └─ The frontend. Every call targets the Gateway URL.
```

**Heuristic:** if answering a request needs data from two services, the request belongs to the service that owns the *primary* entity, and it pulls the secondary data via an HTTP client.

---

## Decision Tree — Which LAYER within a service?

This is fully reusable across every backend service.

```
IS THE CODE...

├─ Accepting/parsing an HTTP request, or choosing a status code?
│  └─ CONTROLLER (thin — no business logic)

├─ Enforcing a business rule or validation?
│  └─ SERVICE (returns Result<T>, never throws for expected failures)

├─ Calling another microservice?
│  └─ A typed CLIENT SERVICE (HttpClient wrapper), invoked from the SERVICE layer

├─ Pushing a real-time update?
│  └─ SERVICE, via IHubContext (broadcast from the service, not the Hub class)

├─ Reading/writing the database?
│  └─ REPOSITORY (the only layer that touches the DbContext)

├─ Shaping data crossing the API boundary?
│  └─ DTO (map entity → DTO before returning; never expose entities)

└─ Defining persistent shape?
   └─ MODEL / ENTITY
```

---

## Naming Conventions

| What | Pattern | Example |
|---|---|---|
| Microservice folder | `{name}-api` / `{name}` | `poll-api`, `gateway` |
| Controller | `{Resource}Controller` | `PollsController` |
| Service class | `{Resource}Service` | `PollService` |
| Inter-service client | `{Remote}ClientService` | `PollClientService` |
| Repository class | `{Resource}Repository` | `PollRepository` |
| DbContext | `{Service}DbContext` | `PollDbContext` |
| Request DTO | `{Action}{Resource}Request` | `CreatePollRequest` |
| Response DTO | `{Resource}Response` | `PollResponse` |
| Entity / Model | PascalCase noun | `Poll`, `Vote`, `User` |
| SignalR Hub | `{Resource}Hub` | `PollHub` |
| Docker image | `pollbuilder-{service}` | `pollbuilder-poll-api` |
| React page | `{Purpose}Page` | `CreatePollPage` |
| React hook | `use{What}` | `useLiveResults` |
| API route | lowercase plural | `/api/polls` |

---

## Design Heuristics (patterns to reach for)

These are *why and when*, not full implementations. For the concrete code, see `pollbuilder-backend`, `pollbuilder-database`, and `pollbuilder-frontend`.

- **`Result<T>` over exceptions.** Expected failures (not found, validation, permission) are return values, not thrown exceptions. Controllers translate `Result<T>` into status codes. Reserve exceptions for the genuinely unexpected (caught by error-handling middleware).
- **Typed `HttpClient` for every inter-service call.** Register with `AddHttpClient<T>()` and a base address from configuration. Never `new HttpClient()`. The client returns `null`/failure on a non-2xx or transport error so the caller degrades gracefully.
- **Validate the reference before writing.** A service recording data that references another domain (e.g. a vote referencing a poll) calls the owning service to confirm the reference is valid and active *before* persisting.
- **Broadcast from the service via `IHubContext`.** The Hub class only manages group membership (join/leave). The service performs the work and pushes the update to the relevant group. Keep broadcast payloads identical to what the REST endpoint returns.
- **Gateway transforms carry identity.** Protected routes extract a claim from the validated JWT and forward it as a header; services read the header instead of parsing tokens.
- **Route specificity ordering.** When concrete routes and a catch-all share a prefix, the specific routes must be ordered first so the catch-all doesn't shadow them.

---

## Heuristic — Adding a New Feature

A repeatable recipe for slotting a feature into the architecture:

1. **Name the data it produces or reads.** Whoever owns that data owns the feature.
2. **Does it fit an existing service's domain?**
   - *Yes* → add a DTO + service method + controller endpoint there, then register a Gateway route, then add a frontend hook + UI, then tests.
   - *No (it's a new bounded domain)* → scaffold a new `services/{name}-api/` with its own DB, DbContext, Dockerfile, Gateway route(s), docker-compose entry, and CI/CD steps. Deploy it independently.
3. **Does it need another service's data?** Add/extend a typed client service rather than reaching into another database.
4. **Does it need to push updates live?** Only then introduce SignalR — and only in the owning service.
5. **Update [ARCHITECTURE.md](../../../ARCHITECTURE.md)** with the new endpoint/route/schema/flow so it stays authoritative.

> Splitting a service later is cheap *only if* you never crossed a database boundary with a direct query. Honoring the Golden Rule from day one keeps that door open.

---

## Review Checklist — Architectural Smells

| ❌ Smell | ✅ Correct | Why |
|---|---|---|
| Two services sharing a database/connection string | One database per service | Data ownership, independent deploys |
| A service querying another's tables directly | Call the owning service's HTTP API | Microservice boundary |
| FK across service databases | Store the reference as a plain value, validate via API | The referenced table is in another DB |
| Service-to-service traffic routed through the Gateway | Call directly via Docker service name | Gateway is for external traffic only |
| JWT validated in every service | Validate at the Gateway, forward an identity header | Single point of auth config |
| One mega-Dockerfile / deploy-everything-together | One Dockerfile and deploy per service | The whole point of microservices |
| `new HttpClient()` inside a service | `AddHttpClient<T>()` via DI | Prevents socket exhaustion |
| Business logic in controllers | Logic in services, controllers stay thin | Testability |
| Returning entities from controllers | Return DTOs mapped from entities | Never leak internals |
| Catch-all route shadowing specific routes | Order specific routes before catch-alls | Correct request matching |
| Hard-coded service URLs | Configuration / env vars | Differs per environment |
| WebSocket proxy missing Upgrade/Connection headers | Include them in Gateway/Nginx | SignalR needs WebSocket proxying |
| Adding SignalR "just in case" | Add real-time only to the service that pushes | Keep services simple |

---

## Cross-References

- **Authoritative project structure, schema, flows, topology** → [ARCHITECTURE.md](../../../ARCHITECTURE.md)
- **Implementing service endpoints** → `pollbuilder-backend`
- **Database per service, schema, migrations** → `pollbuilder-database`
- **Building React components & hooks** → `pollbuilder-frontend`
- **Writing tests** → `pollbuilder-testing`
- **Docker, CI/CD, deployment** → `pollbuilder-devops`
