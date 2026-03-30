# 07 — Deploy

> Target: Railway. Two separate services from one monorepo. PostgreSQL shared.

---

## Current State (2026-03-29)

| Bot | Status | URL |
|---|---|---|
| **LagerthaAssistant** | Production on Railway | — |
| **BaguetteDesign** | Not yet deployed (M1 in progress) | — |

CI/CD pipeline: **not yet configured** (planned in M3 #031)

---

## Infrastructure Overview

```
GitHub (dev → master)
        │
        ▼
  Railway Platform
  ┌─────────────────┐  ┌─────────────────────┐
  │  lagertha        │  │  baguette-design     │
  │  LagerthaAssistant│  │  BaguetteDesign.Api  │
  │  .Api            │  │                     │
  └─────────┬───────┘  └──────────┬──────────┘
            │                     │
            └──────────┬──────────┘
                       ▼
              PostgreSQL (shared)
              baguette_design DB (separate)
```

---

## Dockerfiles

**LagerthaAssistant** (`src/LagerthaAssistant.Api/Dockerfile`):
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish LagerthaAssistant.Api/LagerthaAssistant.Api.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /out .
ENTRYPOINT ["./LagerthaAssistant.Api"]
```
> `libgssapi-krb5-2` is required for Microsoft Graph authentication (Kerberos).

**BaguetteDesign** (`src/BaguetteDesign.Api/Dockerfile`):
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish BaguetteDesign.Api/BaguetteDesign.Api.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["./BaguetteDesign.Api"]
```

---

## Environment Variables

**LagerthaAssistant (Railway):**
```
ConnectionStrings__DefaultConnection=Host=...;Database=lagertha;...
Telegram__BotToken=...
Telegram__Enabled=true
OpenAi__ApiKey=...
Claude__ApiKey=...
Graph__ClientId=...
Graph__ClientSecret=...
Graph__TenantId=...
Lagertha__AdminUserId=...
```

**BaguetteDesign (Railway, planned):**
```
ConnectionStrings__DefaultConnection=Host=...;Database=baguette_design;...
Telegram__BotToken=...
Telegram__Enabled=true
Claude__ApiKey=...
Claude__Model=claude-sonnet-4-6
Baguette__AdminUserId=...
```

---

## Health Check

Both bots expose `GET /health`:

```
GET /health
→ 200 OK
{
  "status": "healthy",
  "db": "connected"   // or "unavailable" if DB is down
}
```

Railway uses this as a liveness probe. If it returns non-200 — deploy is rolled back.

---

## Telegram Webhook

Each bot needs its webhook registered after deploy:

```bash
# Register webhook (replace TOKEN and URL)
curl -X POST "https://api.telegram.org/bot{TOKEN}/setWebhook" \
  -d "url=https://{railway-url}/api/telegram/webhook"

# Verify
curl "https://api.telegram.org/bot{TOKEN}/getWebhookInfo"
```

---

## EF Migrations

Migrations are applied manually (automated migration on startup is not configured).

```bash
# Apply Lagertha migrations
dotnet ef database update \
  --project src/LagerthaAssistant.Infrastructure \
  --startup-project src/LagerthaAssistant.Api

# Apply BaguetteDesign migrations
dotnet ef database update \
  --project src/BaguetteDesign.Infrastructure \
  --startup-project src/BaguetteDesign.Api
```

---

## Planned: GitHub Actions CI/CD (M3 #031)

```yaml
# .github/workflows/ci.yml — on every PR
name: CI
on: [pull_request]
jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '10.x' }
      - run: dotnet build BotPlatform.sln
      - run: dotnet test BotPlatform.sln

# .github/workflows/deploy-baguette.yml — on push to master
# path filter: only if src/BaguetteDesign.* or src/SharedBotKernel changed
```

---

## Pre-deploy Checklist

- [ ] `dotnet test BotPlatform.sln` — all green
- [ ] Migrations applied to target DB
- [ ] `GET /health` → `{"status":"healthy","db":"connected"}`
- [ ] Telegram webhook registered and verified
- [ ] No secrets hardcoded in appsettings.json
- [ ] Tested manually in Telegram: /start as client + as designer
