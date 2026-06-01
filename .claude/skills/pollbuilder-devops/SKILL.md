---
name: pollbuilder-devops
description: Use when deploying, configuring Docker, setting up CI/CD, managing environment variables, or troubleshooting the microservice infrastructure
---

# Poll Builder — DevOps Skill

## Overview

Poll Builder runs as **five Docker containers** orchestrated via docker-compose locally and deployed independently to Render via GitHub Actions. Each microservice has its own Dockerfile, build pipeline, and deployment.

**Principle:** Each microservice is independently buildable, testable, and deployable.

---

## Infrastructure Map

```
LOCAL DEV (docker-compose)                 PRODUCTION (Render)
──────────────────────────                 ─────────────────────
docker-compose up                          GitHub Actions CI/CD
  │                                          │
  ├─ db (SQL Server 2022)                   ├─ lint-and-test (all services)
  │    Port 1433                             ├─ build-and-push (per service)
  │    Hosts: PollDb, VoteDb, IdentityDb    │   → Docker Hub (4 images)
  │                                          └─ deploy (per service)
  ├─ gateway (YARP)                             → Render webhooks
  │    Port 5000 → container 8080
  │                                         Render Services:
  ├─ poll-api (ASP.NET 8)                   ├─ Gateway Web Service
  │    Port 5001 → container 8080           ├─ Poll API Web Service
  │                                         ├─ Vote API Web Service
  ├─ vote-api (ASP.NET 8 + SignalR)         ├─ Identity API Web Service
  │    Port 5002 → container 8080           ├─ Frontend Web Service
  │                                         └─ SQL Server Database
  ├─ identity-api (ASP.NET 8)
  │    Port 5003 → container 8080
  │
  └─ frontend (Nginx)
       Port 5173 → container 80
```

---

## Ports and Hostnames

| Service | Local Port | Container Port | Docker Hostname |
|---|---|---|---|
| SQL Server | 1433 | 1433 | `db` |
| API Gateway | 5000 | 8080 | `gateway` |
| Poll API | 5001 | 8080 | `poll-api` |
| Vote API | 5002 | 8080 | `vote-api` |
| Identity API | 5003 | 8080 | `identity-api` |
| Frontend | 5173 | 80 | `frontend` |

**Critical:** Services call each other by Docker service name (e.g., `http://poll-api:8080`), NOT `localhost`.

---

## Dockerfiles (Per Service)

Each backend service has the same multi-stage Dockerfile pattern. Example for Poll API:

### Backend Dockerfile (template for all services)

**Location:** `services/poll-api/Dockerfile`

```dockerfile
# ── Stage 1: Build ───────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file first (dependency caching)
COPY ["PollApi/PollApi.csproj", "PollApi/"]
RUN dotnet restore "PollApi/PollApi.csproj"

# Copy all source code
COPY . .

# Publish release build
RUN dotnet publish "PollApi/PollApi.csproj" -c Release -o /app/publish

# ── Stage 2: Runtime ─────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "PollApi.dll"]
```

> **Adapt per service:** Change `PollApi` to `VoteApi`, `IdentityApi`, or `Gateway` in each Dockerfile. The pattern is identical.

### Frontend Dockerfile

**Location:** `frontend/Dockerfile`

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

## Nginx Configuration

**Location:** `frontend/nginx.conf`

```nginx
server {
    listen 80;
    root /usr/share/nginx/html;
    index index.html;

    # SPA fallback
    location / {
        try_files $uri $uri/ /index.html;
    }

    # Proxy ALL API requests to the Gateway
    location /api/ {
        proxy_pass http://gateway:8080/api/;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Proxy SignalR to the Gateway (WebSocket upgrade required)
    location /hubs/ {
        proxy_pass http://gateway:8080/hubs/;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}
```

**Important:** Nginx proxies to `gateway`, NOT directly to individual services. The Gateway handles routing and auth.

---

## docker-compose.yml

**Location:** project root

```yaml
services:
  # ── Database (shared instance, separate databases) ─────────
  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      SA_PASSWORD: "YourPassword123!"
      ACCEPT_EULA: "Y"
    ports:
      - "1433:1433"
    volumes:
      - sqldata:/var/opt/mssql

  # ── API Gateway ────────────────────────────────────────────
  gateway:
    build:
      context: ./services/gateway
    environment:
      Jwt__Secret: "dev-secret-min-32-characters-here!"
      Frontend__Url: "http://localhost:5173"
    ports:
      - "5000:8080"
    depends_on:
      - poll-api
      - vote-api
      - identity-api

  # ── Poll API ───────────────────────────────────────────────
  poll-api:
    build:
      context: ./services/poll-api
    environment:
      ConnectionStrings__Default: "Server=db,1433;Database=PollDb;User Id=sa;Password=YourPassword123!;TrustServerCertificate=True;"
    ports:
      - "5001:8080"
    depends_on:
      - db

  # ── Vote API ───────────────────────────────────────────────
  vote-api:
    build:
      context: ./services/vote-api
    environment:
      ConnectionStrings__Default: "Server=db,1433;Database=VoteDb;User Id=sa;Password=YourPassword123!;TrustServerCertificate=True;"
      Services__PollApi: "http://poll-api:8080"
      Gateway__Url: "http://gateway:8080"
    ports:
      - "5002:8080"
    depends_on:
      - db
      - poll-api

  # ── Identity API ───────────────────────────────────────────
  identity-api:
    build:
      context: ./services/identity-api
    environment:
      ConnectionStrings__Default: "Server=db,1433;Database=IdentityDb;User Id=sa;Password=YourPassword123!;TrustServerCertificate=True;"
      Jwt__Secret: "dev-secret-min-32-characters-here!"
    ports:
      - "5003:8080"
    depends_on:
      - db

  # ── Frontend ───────────────────────────────────────────────
  frontend:
    build:
      context: ./frontend
    ports:
      - "5173:80"
    depends_on:
      - gateway

volumes:
  sqldata:
```

---

## Environment Variables

### Per-Service Variables

| Service | Variable | Dev Value | Purpose |
|---|---|---|---|
| **Gateway** | `Jwt__Secret` | `dev-secret-min-32-characters...` | JWT validation |
| **Gateway** | `Frontend__Url` | `http://localhost:5173` | CORS origin |
| **Poll API** | `ConnectionStrings__Default` | `Server=db,...;Database=PollDb;...` | PollDb connection |
| **Vote API** | `ConnectionStrings__Default` | `Server=db,...;Database=VoteDb;...` | VoteDb connection |
| **Vote API** | `Services__PollApi` | `http://poll-api:8080` | Inter-service URL |
| **Vote API** | `Gateway__Url` | `http://gateway:8080` | CORS for SignalR |
| **Identity API** | `ConnectionStrings__Default` | `Server=db,...;Database=IdentityDb;...` | IdentityDb connection |
| **Identity API** | `Jwt__Secret` | `dev-secret-min-32-characters...` | JWT signing |
| **Frontend** | `VITE_API_URL` | `http://localhost:5000/api` | Gateway URL |
| **Frontend** | `VITE_HUB_URL` | `http://localhost:5000/hubs/poll` | SignalR via Gateway |

**Shared secrets:** `Jwt__Secret` MUST be identical in Gateway and Identity API.

---

## GitHub Actions CI/CD

**Location:** `.github/workflows/ci-cd.yml`

```yaml
name: CI/CD

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

env:
  REGISTRY: docker.io
  IMG_GATEWAY: ${{ secrets.DOCKERHUB_USERNAME }}/pollbuilder-gateway
  IMG_POLL: ${{ secrets.DOCKERHUB_USERNAME }}/pollbuilder-poll-api
  IMG_VOTE: ${{ secrets.DOCKERHUB_USERNAME }}/pollbuilder-vote-api
  IMG_IDENTITY: ${{ secrets.DOCKERHUB_USERNAME }}/pollbuilder-identity-api
  IMG_FRONTEND: ${{ secrets.DOCKERHUB_USERNAME }}/pollbuilder-frontend

jobs:
  # ── Phase 1: Lint & Test ────────────────────────────────────
  lint-and-test:
    name: Lint & Test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.x

      # Test each service
      - name: Test Poll API
        run: dotnet test services/poll-api/PollApi.sln --logger trx

      - name: Test Vote API
        run: dotnet test services/vote-api/VoteApi.sln --logger trx

      - name: Test Identity API
        run: dotnet test services/identity-api/IdentityApi.sln --logger trx

      # Frontend
      - name: Setup Node
        uses: actions/setup-node@v4
        with:
          node-version: 20

      - name: Frontend lint
        working-directory: frontend
        run: |
          npm ci
          npm run lint

  # ── Phase 2: Build & Push ALL images ────────────────────────
  build-and-push:
    name: Build & Push
    needs: lint-and-test
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
      - uses: actions/checkout@v4

      - name: Login to Docker Hub
        uses: docker/login-action@v3
        with:
          username: ${{ secrets.DOCKERHUB_USERNAME }}
          password: ${{ secrets.DOCKERHUB_TOKEN }}

      - name: Set up Buildx
        uses: docker/setup-buildx-action@v3

      - name: Build & push Gateway
        uses: docker/build-push-action@v5
        with:
          context: ./services/gateway
          push: true
          tags: ${{ env.IMG_GATEWAY }}:latest
          cache-from: type=gha
          cache-to: type=gha,mode=max

      - name: Build & push Poll API
        uses: docker/build-push-action@v5
        with:
          context: ./services/poll-api
          push: true
          tags: ${{ env.IMG_POLL }}:latest
          cache-from: type=gha
          cache-to: type=gha,mode=max

      - name: Build & push Vote API
        uses: docker/build-push-action@v5
        with:
          context: ./services/vote-api
          push: true
          tags: ${{ env.IMG_VOTE }}:latest
          cache-from: type=gha
          cache-to: type=gha,mode=max

      - name: Build & push Identity API
        uses: docker/build-push-action@v5
        with:
          context: ./services/identity-api
          push: true
          tags: ${{ env.IMG_IDENTITY }}:latest
          cache-from: type=gha
          cache-to: type=gha,mode=max

      - name: Build & push Frontend
        uses: docker/build-push-action@v5
        with:
          context: ./frontend
          push: true
          tags: ${{ env.IMG_FRONTEND }}:latest
          cache-from: type=gha
          cache-to: type=gha,mode=max

  # ── Phase 3: Deploy to Render ───────────────────────────────
  deploy:
    name: Deploy
    needs: build-and-push
    runs-on: ubuntu-latest
    steps:
      - name: Deploy Gateway
        run: curl -X POST "${{ secrets.RENDER_HOOK_GATEWAY }}"
      - name: Deploy Poll API
        run: curl -X POST "${{ secrets.RENDER_HOOK_POLL_API }}"
      - name: Deploy Vote API
        run: curl -X POST "${{ secrets.RENDER_HOOK_VOTE_API }}"
      - name: Deploy Identity API
        run: curl -X POST "${{ secrets.RENDER_HOOK_IDENTITY_API }}"
      - name: Deploy Frontend
        run: curl -X POST "${{ secrets.RENDER_HOOK_FRONTEND }}"
```

### Required GitHub Secrets

| Secret | Purpose |
|---|---|
| `DOCKERHUB_USERNAME` | Docker Hub login |
| `DOCKERHUB_TOKEN` | Docker Hub auth |
| `RENDER_HOOK_GATEWAY` | Gateway deploy webhook |
| `RENDER_HOOK_POLL_API` | Poll API deploy webhook |
| `RENDER_HOOK_VOTE_API` | Vote API deploy webhook |
| `RENDER_HOOK_IDENTITY_API` | Identity API deploy webhook |
| `RENDER_HOOK_FRONTEND` | Frontend deploy webhook |

---

## Local Development Workflow

### First Time Setup

```bash
# 1. Clone and start
git clone <repo-url> && cd poll-service
docker-compose up --build

# 2. Wait for services (check logs)
docker-compose logs -f

# 3. Apply migrations for each service
docker-compose exec poll-api dotnet ef database update
docker-compose exec vote-api dotnet ef database update
docker-compose exec identity-api dotnet ef database update

# 4. Verify
#    Frontend:    http://localhost:5173
#    Gateway:     http://localhost:5000/api/polls
#    Poll API:    http://localhost:5001/api/polls
#    Vote API:    http://localhost:5002/api/polls/{code}/results
#    Identity:    http://localhost:5003/api/auth/register
```

### Rebuilding a Single Service

```bash
# Only rebuild vote-api after code changes
docker-compose up --build vote-api

# Rebuild everything
docker-compose up --build
```

---

## Troubleshooting Decision Tree

```
PROBLEM: Service can't connect to database
├─ Check hostname is "db" not "localhost"
├─ Check Database= name matches per-service (PollDb, VoteDb, IdentityDb)
├─ Is SQL Server ready? (takes 10-30s to initialize)
└─ docker-compose logs db

PROBLEM: Inter-service call fails (Vote API → Poll API)
├─ Is Poll API running? docker-compose ps
├─ Is Services__PollApi set to "http://poll-api:8080"?
├─ Can Vote API resolve "poll-api"? (Docker DNS)
└─ docker-compose logs vote-api

PROBLEM: Gateway returns 502 Bad Gateway
├─ Is the target service running?
├─ Are YARP routes correct? Check appsettings.json
├─ Is the cluster destination correct? (http://poll-api:8080)
└─ docker-compose logs gateway

PROBLEM: SignalR connection fails
├─ Is nginx configured for WebSocket? (Upgrade headers)
├─ Is Gateway proxying /hubs/ to vote-api?
├─ Is Vote API CORS set with AllowCredentials?
└─ Check browser dev tools Network tab for WS errors

PROBLEM: JWT validation fails at Gateway
├─ Is Jwt__Secret identical in Gateway and Identity API?
├─ Is token expired? (check exp claim)
└─ docker-compose logs gateway
```

---

## Security Best Practices

| ✅ Do | ❌ Don't |
|---|---|
| Keep `Jwt__Secret` in sync (Gateway + Identity) | Commit secrets to git |
| Use multi-stage Docker builds | Ship SDK in production images |
| Keep services internal (only Gateway exposed) | Expose all service ports in production |
| Use `.dockerignore` per service | Include `.git`/`node_modules` in builds |
| Scan images for vulnerabilities | Skip security scanning |

---

## Cross-References

- **Service implementations** → `pollbuilder-backend/SKILL.md`
- **Database per service** → `pollbuilder-database/SKILL.md`
- **Frontend** → `pollbuilder-frontend/SKILL.md`
- **Testing** → `pollbuilder-testing/SKILL.md`
- **Architecture overview** → `pollbuilder-architecture/SKILL.md`
