# 07 — Deploy

> Target: Railway. Two separate services from one monorepo. PostgreSQL shared.

---

## Current State (2026-03-31)

| Bot | Status | URL |
|---|---|---|
| **LagerthaAssistant** | Production on Railway | — |
| **BaguetteDesign** | Ready to deploy (M1+M2 merged, runbook ready) | — |

CI/CD pipeline: **configured** — `.github/workflows/ci.yml` (PR checks) + `.github/workflows/deploy-baguette.yml` (manual, currently paused by guard variable)

> Детальна покрокова інструкція деплою BaguetteDesign: `docs/21-baguette-deploy-runbook.md`

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

For fullscreen-capable Telegram `Settings` in Lagertha, configure either `Telegram__MiniAppSettingsDirectUrl` or `Telegram__BotUsername`. The derived main Mini App link now uses `?startapp=settings&mode=fullscreen`, so the bot should also have its Main Mini App configured in BotFather.

**BaguetteDesign (Railway):**
```
ConnectionStrings__DefaultConnection=Host=...;Database=baguette_design;...
Telegram__BotToken=...
Telegram__Enabled=true
Telegram__WebhookSecret=...
Claude__ApiKey=...
Claude__Model=claude-sonnet-4-6
Baguette__AdminUserId=...
```

---

## BotFather Main Mini App Setup (Lagertha)

For Lagertha `Settings`, a regular reply-keyboard `web_app` button is not enough to guarantee fullscreen-capable launch behavior. The bot should also have its **Main Mini App** configured in BotFather.

### Preconditions

Before opening BotFather, make sure:

1. Railway variables are configured:
   - `Telegram__BotUsername=LagerthaAssistantBot`
   - optionally `Telegram__MiniAppSettingsDirectUrl=https://t.me/LagerthaAssistantBot?startapp=settings&mode=fullscreen`
2. The public Mini App URL opens successfully:
   - `https://lagertha-prod.up.railway.app/miniapp/settings`
3. The service has been redeployed after env var changes.

### BotFather Steps

1. Open [@BotFather](https://t.me/BotFather)
2. Run `/mybots`
3. Select `@LagerthaAssistantBot`
4. Open `Bot Settings`
5. Open `Configure Mini App`
6. Enable Mini App if it is not enabled yet
7. When BotFather asks for the Mini App URL, provide:
   - `https://lagertha-prod.up.railway.app/miniapp/settings`
8. Save the configuration

### Post-config Verification

1. Open the bot chat and run `/start`
2. Trigger `Налаштування`
3. Confirm that the launch-entry message appears
4. Confirm that the launch-entry prefers the fullscreen-capable direct flow instead of the old fallback button
5. Test the direct link manually:
   - `https://t.me/LagerthaAssistantBot?startapp=settings&mode=fullscreen`

### Expected Result

- the bot can build a direct Mini App entry link
- Telegram has Main Mini App configuration for the bot
- `Settings` can use the fullscreen-capable launch path

### If It Still Does Not Work

Check these in order:

1. `Telegram__BotUsername` is set correctly on Railway
2. `Telegram__MiniAppSettingsDirectUrl` is correct if set explicitly
3. BotFather Main Mini App URL matches the production `/miniapp/settings` URL
4. The latest `master` deploy is active on Railway
5. Telegram client cache was refreshed by reopening the chat or running `/start`

If all five are correct and fullscreen still does not happen, investigate Telegram client/version behavior next rather than changing CSS or frontend layout first.

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

## GitHub Actions CI/CD

Workflows у `.github/workflows/`:

- **`ci.yml`** — запускається на кожен PR у master і push у dev: restore → build → test
- **`deploy-baguette.yml`** — запускається вручну (`workflow_dispatch`), і додатково має guard `if: vars.ENABLE_BAGUETTE_DEPLOY == 'true'`.
  Поточне правило: **Baguette deploy pause** (поки тестуємо Lagertha, деплой Baguette вимкнений).

Потрібний secret у GitHub: `RAILWAY_TOKEN` (Railway → Account Settings → Tokens).

---

## Pre-deploy Checklist

- [ ] `dotnet test LagerthaAssistant.sln` — all green (945/945)
- [ ] Migrations applied to target DB (8 міграцій BaguetteDesign)
- [ ] `GET /health` → `{"status":"healthy","db":"connected"}`
- [ ] Telegram webhook registered and verified (`setWebhook` + `secret_token`)
- [ ] No secrets hardcoded in appsettings.json
- [ ] `Telegram__WebhookSecret` встановлено на Railway
- [ ] Tested manually in Telegram: /start as client + as designer
