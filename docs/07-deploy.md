# 07 â€” Deploy

> Target: Railway. Two separate services from one monorepo. PostgreSQL shared.

---

## Current State (2026-03-31)

| Bot | Status | URL |
|---|---|---|
| **LagerthaAssistant** | Production on Railway | â€” |
| **BaguetteDesign** | Ready to deploy (M1+M2 merged, runbook ready) | â€” |

CI/CD pipeline: **configured** â€” `.github/workflows/ci.yml` (PR checks) + `.github/workflows/deploy-baguette.yml` (manual, currently paused by guard variable)

> Ð”ÐµÑ‚Ð°Ð»ÑŒÐ½Ð° Ð¿Ð¾ÐºÑ€Ð¾ÐºÐ¾Ð²Ð° Ñ–Ð½ÑÑ‚Ñ€ÑƒÐºÑ†Ñ–Ñ Ð´ÐµÐ¿Ð»Ð¾ÑŽ BaguetteDesign: `docs/21-baguette-deploy-runbook.md`

---

## Infrastructure Overview

```
GitHub (dev â†’ master)
        â”‚
        â–¼
  Railway Platform
  â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”  â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
  â”‚  lagertha        â”‚  â”‚  baguette-design     â”‚
  â”‚  LagerthaAssistantâ”‚  â”‚  BaguetteDesign.Api  â”‚
  â”‚  .Api            â”‚  â”‚                     â”‚
  â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”˜  â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
            â”‚                     â”‚
            â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
                       â–¼
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

For direct Mini App Telegram `Settings` in Lagertha, configure either `Telegram__MiniAppSettingsDirectUrl` or `Telegram__BotUsername`. The derived main Mini App link now uses `?startapp=settings&mode=compact`, so the bot should also have its Main Mini App configured in BotFather.

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

For Lagertha `Settings`, a regular reply-keyboard `web_app` button is not enough to guarantee direct Mini App launch behavior. The bot should also have its **Main Mini App** configured in BotFather.

### Preconditions

Before opening BotFather, make sure:

1. Railway variables are configured:
   - `Telegram__BotUsername=LagerthaAssistantBot`
   - optionally `Telegram__MiniAppSettingsDirectUrl=https://t.me/LagerthaAssistantBot?startapp=settings&mode=compact`
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
2. Trigger `ÐÐ°Ð»Ð°ÑˆÑ‚ÑƒÐ²Ð°Ð½Ð½Ñ`
3. Confirm that the launch-entry message appears
4. Confirm that the launch-entry prefers the direct Mini App direct flow instead of the old fallback button
5. Test the direct link manually:
   - `https://t.me/LagerthaAssistantBot?startapp=settings&mode=compact`

### Expected Result

- the bot can build a direct Mini App entry link
- Telegram has Main Mini App configuration for the bot
- `Settings` can use the direct Mini App launch path

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
â†’ 200 OK
{
  "status": "healthy",
  "db": "connected"   // or "unavailable" if DB is down
}
```

Railway uses this as a liveness probe. If it returns non-200 â€” deploy is rolled back.

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

Workflows Ñƒ `.github/workflows/`:

- **`ci.yml`** â€” Ð·Ð°Ð¿ÑƒÑÐºÐ°Ñ”Ñ‚ÑŒÑÑ Ð½Ð° ÐºÐ¾Ð¶ÐµÐ½ PR Ñƒ master Ñ– push Ñƒ dev: restore â†’ build â†’ test
- **`deploy-baguette.yml`** â€” Ð·Ð°Ð¿ÑƒÑÐºÐ°Ñ”Ñ‚ÑŒÑÑ Ð²Ñ€ÑƒÑ‡Ð½Ñƒ (`workflow_dispatch`), Ñ– Ð´Ð¾Ð´Ð°Ñ‚ÐºÐ¾Ð²Ð¾ Ð¼Ð°Ñ” guard `if: vars.ENABLE_BAGUETTE_DEPLOY == 'true'`.
  ÐŸÐ¾Ñ‚Ð¾Ñ‡Ð½Ðµ Ð¿Ñ€Ð°Ð²Ð¸Ð»Ð¾: **Baguette deploy pause** (Ð¿Ð¾ÐºÐ¸ Ñ‚ÐµÑÑ‚ÑƒÑ”Ð¼Ð¾ Lagertha, Ð´ÐµÐ¿Ð»Ð¾Ð¹ Baguette Ð²Ð¸Ð¼ÐºÐ½ÐµÐ½Ð¸Ð¹).

ÐŸÐ¾Ñ‚Ñ€Ñ–Ð±Ð½Ð¸Ð¹ secret Ñƒ GitHub: `RAILWAY_TOKEN` (Railway â†’ Account Settings â†’ Tokens).

---

## Pre-deploy Checklist

- [ ] `dotnet test LagerthaAssistant.sln` â€” all green (945/945)
- [ ] Migrations applied to target DB (8 Ð¼Ñ–Ð³Ñ€Ð°Ñ†Ñ–Ð¹ BaguetteDesign)
- [ ] `GET /health` â†’ `{"status":"healthy","db":"connected"}`
- [ ] Telegram webhook registered and verified (`setWebhook` + `secret_token`)
- [ ] No secrets hardcoded in appsettings.json
- [ ] `Telegram__WebhookSecret` Ð²ÑÑ‚Ð°Ð½Ð¾Ð²Ð»ÐµÐ½Ð¾ Ð½Ð° Railway
- [ ] Tested manually in Telegram: /start as client + as designer

