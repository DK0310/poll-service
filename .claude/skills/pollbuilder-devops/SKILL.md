---
name: pollbuilder-devops
description: Use when deploying, configuring Docker, setting up CI/CD, managing environment variables, or troubleshooting the microservice infrastructure
---

# Poll Builder — DevOps Skill

This skill holds **reusable Docker, CI/CD, and operations patterns**: the multi-stage Dockerfile template, the Nginx WebSocket-proxy config, compose/pipeline patterns, a troubleshooting decision tree, and a security checklist.

> **Project facts live in [ARCHITECTURE.md](../../../ARCHITECTURE.md)** — the service topology and ports/hostnames, the docker-compose layout, the CI/CD pipeline phases, the per-service environment variables, and the required GitHub secrets. The real files are `docker-compose.yml` and `.github/workflows/ci-cd.yml` at the repo root. This skill does not repeat their contents.

---

## Core Principle

**Each microservice is independently buildable, testable, and deployable.** One Dockerfile, one image, one deploy target per service. A change to one service rebuilds and redeploys only that service's image.

---

## Multi-Stage Dockerfile (backend template)

Identical pattern for every backend service — swap the project name (`PollApi` → `VoteApi` / `IdentityApi` / `Gateway`). Multi-stage keeps the SDK out of the runtime image (~200 MB instead of ~900 MB).

```dockerfile
# ── Stage 1: Build ───────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the csproj first so `restore` is cached until dependencies change
COPY ["PollApi/PollApi.csproj", "PollApi/"]
RUN dotnet restore "PollApi/PollApi.csproj"

COPY . .
RUN dotnet publish "PollApi/PollApi.csproj" -c Release -o /app/publish

# ── Stage 2: Runtime ─────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "PollApi.dll"]
```

**Reusable techniques:** copy the project file and `restore` before copying source (layer caching); build in the SDK image, run in the slim ASP.NET image; `EXPOSE` the container port (all services listen on 8080 internally).

### Frontend Dockerfile (build → static serve)

```dockerfile
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json .
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```

---

## Nginx Pattern — SPA fallback + proxy to the Gateway

The frontend container proxies API and WebSocket traffic to the **Gateway only** (never to individual services). The `/hubs/` block needs the WebSocket upgrade headers or SignalR fails.

```nginx
server {
    listen 80;
    root /usr/share/nginx/html;
    index index.html;

    # SPA fallback — let the client router handle unknown paths
    location / {
        try_files $uri $uri/ /index.html;
    }

    # REST → Gateway
    location /api/ {
        proxy_pass http://gateway:8080/api/;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # SignalR → Gateway (WebSocket upgrade REQUIRED)
    location /hubs/ {
        proxy_pass http://gateway:8080/hubs/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }
}
```

**The WebSocket rule:** any proxy in front of SignalR (Nginx *and* the Gateway) must forward `Upgrade` + `Connection: upgrade` and use HTTP/1.1.

---

## docker-compose Patterns

The full service list, ports, and env values are in [ARCHITECTURE.md](../../../ARCHITECTURE.md); the file is `docker-compose.yml`. Patterns to apply when editing it:

- **One shared `db` service, separate databases.** All API services point at `Server=db,1433;Database=<ServiceDb>;...` — same host, different `Database=`.
- **Service-name networking.** Services reach each other by compose service name (`http://poll-api:8080`), never `localhost`.
- **`depends_on` for boot order.** APIs depend on `db`; the Gateway depends on the APIs; the frontend depends on the Gateway. (Order ≠ readiness — SQL Server still needs ~10–30 s to accept connections.)
- **Port mapping `external:8080`.** Every backend container listens on 8080; map distinct host ports (see ARCHITECTURE.md).
- **Per-service `environment:`** blocks — only the variables that service needs.

---

## CI/CD Patterns (GitHub Actions)

The pipeline phases and required secrets are in [ARCHITECTURE.md](../../../ARCHITECTURE.md); the file is `.github/workflows/ci-cd.yml`. Reusable patterns:

- **Gate builds behind lint + test.** `build-and-push` `needs: lint-and-test`; the build job is `if: github.ref == 'refs/heads/main'` so PRs run tests only.
- **Test each service independently** (`dotnet test <Service>.sln`) and lint the frontend (`npm ci && npm run lint`) — satisfies the Merit "linting/static-analysis step".
- **Build & push per service** with `docker/build-push-action`, one step per image, using GitHub Actions layer cache:
  ```yaml
  cache-from: type=gha
  cache-to: type=gha,mode=max
  ```
- **Deploy via a webhook per service** — `curl -X POST "$RENDER_HOOK_<SERVICE>"`, one step each, so services deploy independently.
- **Reference secrets, never inline them** — `${{ secrets.DOCKERHUB_TOKEN }}`, etc.

---

## Local Development Workflow

```bash
# Start everything
docker-compose up --build

# Apply migrations once SQL Server is ready (per service)
docker-compose exec poll-api     dotnet ef database update
docker-compose exec vote-api     dotnet ef database update
docker-compose exec identity-api dotnet ef database update

# Rebuild a single service after a code change
docker-compose up --build vote-api
```

Verification URLs (frontend, gateway, each API) are listed in [ARCHITECTURE.md](../../../ARCHITECTURE.md).

---

## Troubleshooting Decision Tree

```
PROBLEM: Service can't connect to the database
├─ Hostname is "db", not "localhost"?
├─ Database= matches the service (PollDb / VoteDb / IdentityDb)?
├─ SQL Server finished initializing? (10–30 s)
└─ docker-compose logs db

PROBLEM: Inter-service call fails (Vote API → Poll API)
├─ Is the target service running? docker-compose ps
├─ Is Services__PollApi = "http://poll-api:8080"?
├─ Can the caller resolve the service name? (Docker DNS)
└─ docker-compose logs vote-api

PROBLEM: Gateway returns 502 Bad Gateway
├─ Is the target service up?
├─ Are the YARP routes/orders correct? (see ARCHITECTURE.md routing table)
├─ Is the cluster address right? (http://<service>:8080)
└─ docker-compose logs gateway

PROBLEM: SignalR connection fails
├─ Nginx forwarding Upgrade/Connection headers on /hubs/?
├─ Gateway proxying /hubs/ to vote-api?
├─ Vote API CORS set with AllowCredentials?
└─ Browser dev tools → Network → WS frames

PROBLEM: JWT validation fails at the Gateway
├─ Is Jwt__Secret identical in Gateway and Identity API?
├─ Token expired? (check exp claim)
└─ docker-compose logs gateway
```

---

## Security Best Practices

| ✅ Do | ❌ Don't |
|---|---|
| Keep `Jwt__Secret` identical in Gateway + Identity | Commit secrets to git |
| Use multi-stage Docker builds | Ship the SDK in production images |
| Expose only the Gateway + frontend | Publish every service's port in prod |
| Add a `.dockerignore` per service | Bundle `.git` / `node_modules` into images |
| Pull secrets from the platform's store | Hard-code credentials in compose/CI |
| Scan images for vulnerabilities | Skip security scanning |

---

## Cross-References

- **Authoritative topology, ports, env vars, pipeline, secrets** → [ARCHITECTURE.md](../../../ARCHITECTURE.md)
- **System design principles** → `pollbuilder-architecture`
- **Service implementations** → `pollbuilder-backend`
- **Database migrations** → `pollbuilder-database`
- **Frontend build** → `pollbuilder-frontend`
- **Tests run in CI** → `pollbuilder-testing`
