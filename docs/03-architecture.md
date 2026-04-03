# ÐÑ€Ñ…Ñ–Ñ‚ÐµÐºÑ‚ÑƒÑ€Ð°: BotPlatform

> Ð’ÐµÑ€ÑÑ–Ñ: 1.1 | Ð¡Ñ‚Ð°Ñ‚ÑƒÑ: ÐžÐ½Ð¾Ð²Ð»ÐµÐ½Ð¾ (SharedBotKernel Ñ€ÐµÐ°Ð»Ñ–Ð·Ð¾Ð²Ð°Ð½Ð¾)
> Ð§Ð¸Ñ‚Ð°Ñ‚Ð¸ Ñ€Ð°Ð·Ð¾Ð¼ Ð·: docs/adr/*.md

---

## Monorepo ÑÑ‚Ñ€ÑƒÐºÑ‚ÑƒÑ€Ð° (BotPlatform.sln)

```
BotPlatform.sln
â”‚
â”œâ”€â”€ src/SharedBotKernel/          â† Ð¡Ð¿Ñ–Ð»ÑŒÐ½Ð° Ð±Ñ–Ð±Ð»Ñ–Ð¾Ñ‚ÐµÐºÐ° (Ñ€ÐµÐ°Ð»Ñ–Ð·Ð¾Ð²Ð°Ð½Ð¾)
â”‚   â”œâ”€â”€ Domain/                   â† Entities, Base classes, AI types
â”‚   â”œâ”€â”€ Infrastructure/AI/        â† ClaudeChatClient, OpenAiChatClient
â”‚   â””â”€â”€ Persistence/              â† KernelDbContext (abstract)
â”‚
â”œâ”€â”€ src/LagerthaAssistant.*/      â† ÐŸÐ¾Ð±ÑƒÑ‚Ð¾Ð²Ð¸Ð¹ Ð±Ð¾Ñ‚ (production)
â”‚   â”œâ”€â”€ LagerthaAssistant.Domain
â”‚   â”œâ”€â”€ LagerthaAssistant.Application
â”‚   â”œâ”€â”€ LagerthaAssistant.Infrastructure  â† AppDbContext : KernelDbContext
â”‚   â””â”€â”€ LagerthaAssistant.Api
â”‚
â””â”€â”€ src/BaguetteDesign.*/         â† Ð‘Ð¾Ñ‚ Ð´Ð»Ñ Ð´Ð¸Ð·Ð°Ð¹Ð½ÐµÑ€Ð° (M1+)
    â”œâ”€â”€ BaguetteDesign.Domain
    â”œâ”€â”€ BaguetteDesign.Application
    â”œâ”€â”€ BaguetteDesign.Infrastructure     â† BaguetteDbContext : KernelDbContext
    â””â”€â”€ BaguetteDesign.Api
```

---

## C4 Level 1: System Context (BaguetteDesign)

```
[ÐšÐ»Ñ–Ñ”Ð½Ñ‚ (Telegram)]  â†’  [BaguetteDesign Bot]  â†’  [Notion]
[Ð”Ð¸Ð·Ð°Ð¹Ð½ÐµÑ€ (Telegram)] â†’  [BaguetteDesign Bot]  â†’  [Google Drive]
                                                â†’  [Google Calendar]
                                                â†’  [Claude AI (Anthropic)]
                                                â†’  [PostgreSQL]
```

---

## C4 Level 2: Container Diagram

```
BaguetteDesign (Railway.app)
â”‚
â”œâ”€â”€ BaguetteDesign.Api           â† ASP.NET Core 10, Webhook endpoint
â”‚   POST /api/telegram/webhook
â”‚   GET  /health
â”‚
â”œâ”€â”€ BaguetteDesign.Application   â† Use-case services
â”‚   BriefFlowService             â† ÐŸÐ¾ÐºÑ€Ð¾ÐºÐ¾Ð²Ð¸Ð¹ Ð±Ñ€Ð¸Ñ„
â”‚   InboxService                 â† Inbox Ð´Ð¸Ð·Ð°Ð¹Ð½ÐµÑ€Ð°
â”‚   LeadService                  â† CRM
â”‚   ProjectService               â† ÐŸÑ€Ð¾Ñ”ÐºÑ‚Ð¸ + ÑÑ‚Ð°Ñ‚ÑƒÑÐ¸
â”‚   FileService                  â† ÐœÐ°Ñ‚ÐµÑ€Ñ–Ð°Ð»Ð¸ ÐºÐ»Ñ–Ñ”Ð½Ñ‚Ñ–Ð²
â”‚   PriceService                 â† ÐŸÑ€Ð°Ð¹Ñ Ð· Notion
â”‚   PortfolioService             â† ÐŸÐ¾Ñ€Ñ‚Ñ„Ð¾Ð»Ñ–Ð¾ Ð· Notion
â”‚   CalendarService              â† Ð”Ð·Ð²Ñ–Ð½ÐºÐ¸
â”‚   NotificationService          â† Ð Ð¾Ð·ÑƒÐ¼Ð½Ñ– Ð½Ð°Ð³Ð°Ð´ÑƒÐ²Ð°Ð½Ð½Ñ
â”‚   AiAssistantService           â† AI Ð´Ð»Ñ Ð´Ð¸Ð·Ð°Ð¹Ð½ÐµÑ€Ð°
â”‚
â”œâ”€â”€ BaguetteDesign.Infrastructure â† Adapters
â”‚   BaguetteDbContext             â† EF Core â†’ PostgreSQL
â”‚   GoogleDriveClient             â† Google Drive API
â”‚   GoogleCalendarClient          â† Google Calendar API
â”‚   NotionBriefClient             â† Notion (briefs, leads, projects)
â”‚   NotionPriceClient             â† Notion (price + portfolio cache)
â”‚   ReminderWorker                â† BackgroundSyncWorkerBase<Notification>
â”‚
â””â”€â”€ BaguetteDesign.Domain        â† Entities, Value Objects, Interfaces
    Client, Lead, Brief, Project
    Message, FileRecord, CalendarEvent
    Notification, Tenant
    PriceItem, PortfolioCase

SharedBotKernel (Project Reference)
â”‚
â”œâ”€â”€ Domain
â”‚   ConversationSession, ConversationHistoryEntry
â”‚   UserMemoryEntry, SystemPromptEntry, SystemPromptProposal
â”‚   ConversationIntentMetric, TelegramProcessedUpdate
â”‚
â”œâ”€â”€ Infrastructure
â”‚   ClaudeChatClient, OpenAiChatClient, ResolvingAiChatClient
â”‚   NotionHttpClient (Ð±Ð°Ð·Ð¾Ð²Ð¸Ð¹ HTTP wrapper)
â”‚   GraphHttpClient, TokenManager, GraphAuthService
â”‚   TelegramWebhookAdapter, TelegramDeduplicationService
â”‚   IClock, SystemClock
â”‚
â”œâ”€â”€ Persistence
â”‚   KernelDbContext (abstract)
â”‚
â””â”€â”€ Workers
    BackgroundSyncWorkerBase<TJob>

External Services
â”œâ”€â”€ PostgreSQL (Railway) â€” ÑÐ¿Ñ–Ð»ÑŒÐ½Ð¸Ð¹ Ð´Ð»Ñ Ð¾Ð±Ð¾Ñ… Ð±Ð¾Ñ‚Ñ–Ð²
â”œâ”€â”€ Anthropic Claude API
â”œâ”€â”€ Notion API (v2022-06-28)
â”œâ”€â”€ Google Drive API v3
â””â”€â”€ Google Calendar API v3
```

---

## Telegram Message Processing Flow

```
Telegram â†’ POST /api/telegram/webhook
  â†“
TelegramWebhookAdapter (SharedBotKernel)
  â†“
TelegramDeduplicationService â†’ TelegramProcessedUpdates
  â†“
RoleRouter.Resolve(userId) â†’ Designer | Client
  â†“
  â”œâ”€â”€ Designer â†’ DesignerUpdateRouter
  â”‚     â”œâ”€â”€ InboxController
  â”‚     â”œâ”€â”€ LeadsController
  â”‚     â”œâ”€â”€ ProjectsController
  â”‚     â””â”€â”€ QuickActionController
  â”‚
  â””â”€â”€ Client â†’ ClientUpdateRouter
        â”œâ”€â”€ StartHandler
        â”œâ”€â”€ QuestionHandler (Claude)
        â”œâ”€â”€ BriefFlowHandler (state machine)
        â”œâ”€â”€ PriceHandler (Notion)
        â”œâ”€â”€ PortfolioHandler (Notion)
        â”œâ”€â”€ ContactHandler (Calendar)
        â””â”€â”€ StatusHandler
```

---

## Ð‘Ð°Ð·Ð° Ð´Ð°Ð½Ð¸Ñ…: BaguetteDbContext

### Ð¢Ð°Ð±Ð»Ð¸Ñ†Ñ– SharedBotKernel (ÑƒÑÐ¿Ð°Ð´ÐºÐ¾Ð²ÑƒÑŽÑ‚ÑŒÑÑ Ñ‡ÐµÑ€ÐµÐ· KernelDbContext)

| Ð¢Ð°Ð±Ð»Ð¸Ñ†Ñ | ÐŸÑ€Ð¸Ð·Ð½Ð°Ñ‡ÐµÐ½Ð½Ñ |
|---|---|
| ConversationSessions | ÐžÐ´Ð½Ð° ÑÐµÑÑ–Ñ Ð½Ð° scope (channel+userId+conversationId) |
| ConversationHistoryEntries | ÐŸÐ¾Ð²Ð½Ð° Ð¿ÐµÑ€ÐµÐ¿Ð¸ÑÐºÐ° ÑÐµÑÑ–Ñ— |
| UserMemoryEntries | Key/Value Ð¿Ð°Ð¼'ÑÑ‚ÑŒ Ð· Confidence |
| SystemPromptEntries | Ð’ÐµÑ€ÑÑ–Ð¾Ð½Ð¾Ð²Ð°Ð½Ð¸Ð¹ system prompt Claude |
| SystemPromptProposals | ÐŸÑ€Ð¾Ð¿Ð¾Ð·Ð¸Ñ†Ñ–Ñ— Ð·Ð¼Ñ–Ð½ Ð¿Ñ€Ð¾Ð¼Ð¿Ñ‚Ñƒ |
| ConversationIntentMetrics | ÐÐ½Ð°Ð»Ñ–Ñ‚Ð¸ÐºÐ° Ð¿Ð¾ intent/agent |
| TelegramProcessedUpdates | Ð”ÐµÐ´ÑƒÐ¿Ð»Ñ–ÐºÐ°Ñ†Ñ–Ñ webhook (UpdateId PK) |

### Ð¢Ð°Ð±Ð»Ð¸Ñ†Ñ– BaguetteDesign-specific

#### tenants
```sql
Id          UUID PK
OwnerId     UUID FK â†’ users (Telegram designer)
Name        VARCHAR(256)      -- "Ð¡Ñ‚ÑƒÐ´Ñ–Ñ Lesia Design"
InviteRef   VARCHAR(64) UNIQUE -- Ð´Ð»Ñ t.me/bot?start=ref_xxx
CreatedAt   TIMESTAMPTZ
```

#### designer_clients
```sql
Id          UUID PK
TenantId    UUID              -- Ð—ÐÐ’Ð–Ð”Ð˜ Ð¿Ñ€Ð¸ÑÑƒÑ‚Ð½Ñ–Ð¹ (ADR-004)
TgId        BIGINT            -- Telegram user_id
Name        VARCHAR(256)
Contact     VARCHAR(256)
Country     VARCHAR(64)
Tags        TEXT[]            -- VIP, Ð¿Ð¾Ð²Ñ‚Ð¾Ñ€Ð½Ð¸Ð¹, Ñ…Ð¾Ð»Ð¾Ð´Ð½Ð¸Ð¹
Source      VARCHAR(64)       -- telegram | referral | manual
CreatedAt   TIMESTAMPTZ
UpdatedAt   TIMESTAMPTZ
```

#### leads
```sql
Id           UUID PK
TenantId     UUID
ClientId     UUID FK â†’ designer_clients
ServiceType  VARCHAR(128)     -- logo | identity | social | print | web
BudgetRange  VARCHAR(64)      -- "<500" | "500-1000" | ">1000"
Country      VARCHAR(64)
Status       VARCHAR(32)      -- new | negotiation | contract | rejected
BriefComplete BOOLEAN DEFAULT FALSE
Source       VARCHAR(64)
NotionPageId  VARCHAR(128)    -- Notion sync
CreatedAt    TIMESTAMPTZ
UpdatedAt    TIMESTAMPTZ
```

**Indexes:** (TenantId, Status), (TenantId, ClientId)

#### briefs
```sql
Id              UUID PK
TenantId        UUID
LeadId          UUID FK â†’ leads
ClientId        UUID FK â†’ designer_clients
RawDialog       JSONB         -- Ð²ÐµÑÑŒ Ð´Ñ–Ð°Ð»Ð¾Ð³ Ð±Ñ€Ð¸Ñ„Ñƒ [{role, content, timestamp}]
StructuredData  JSONB         -- {service_type, brand, audience, style, deadline, budget, country, ...}
AiAnalysis      JSONB         -- {completeness_score, missing_fields[], recommendations[]}
IsComplete      BOOLEAN
NotionPageId    VARCHAR(128)
CreatedAt       TIMESTAMPTZ
UpdatedAt       TIMESTAMPTZ
```

#### projects
```sql
Id              UUID PK
TenantId        UUID
ClientId        UUID FK â†’ designer_clients
LeadId          UUID FK â†’ leads  NULLABLE
BriefId         UUID FK â†’ briefs NULLABLE
Title           VARCHAR(256)
ServiceType     VARCHAR(128)
Status          VARCHAR(32)   -- brief|contract|in_progress|review|final|done
Deadline        DATE
RevisionCount   INT DEFAULT 0
MaxRevisions    INT DEFAULT 2
NotionPageId    VARCHAR(128)
DriveFolderId   VARCHAR(256)
CreatedAt       TIMESTAMPTZ
UpdatedAt       TIMESTAMPTZ
```

**Indexes:** (TenantId, Status), (TenantId, ClientId)

#### messages
```sql
Id            UUID PK
TenantId      UUID
ClientId      UUID FK â†’ designer_clients
ProjectId     UUID FK â†’ projects NULLABLE
Role          VARCHAR(32)   -- client | bot | designer
Content       TEXT
TgMessageId   BIGINT        -- Ð´Ð»Ñ reply/forward
CreatedAt     TIMESTAMPTZ
```

**Index:** (TenantId, ClientId, CreatedAt DESC)

#### file_records
```sql
Id              UUID PK
TenantId        UUID
ProjectId       UUID FK â†’ projects NULLABLE
ClientId        UUID FK â†’ designer_clients
FileType        VARCHAR(64)  -- logo | reference | brandbook | text | final
OriginalName    VARCHAR(256)
DriveFileId     VARCHAR(256) -- Google Drive file ID
TgFileId        VARCHAR(256) -- Telegram file_id Ð´Ð»Ñ download
Url             TEXT         -- Ð¿Ñ€ÑÐ¼Ð¸Ð¹ Google Drive link
UploadedAt      TIMESTAMPTZ
```

#### calendar_events
```sql
Id              UUID PK
TenantId        UUID
ClientId        UUID FK â†’ designer_clients
ProjectId       UUID FK â†’ projects NULLABLE
Type            VARCHAR(64)  -- call | deadline | reminder
GoogleCalId     VARCHAR(256)
ScheduledAt     TIMESTAMPTZ
MeetLink        TEXT
Status          VARCHAR(32)  -- pending | confirmed | cancelled
CreatedAt       TIMESTAMPTZ
```

#### notifications
```sql
Id            UUID PK
TenantId      UUID
TargetRole    VARCHAR(32)  -- designer | client
TargetTgId    BIGINT       -- ÐºÐ¾Ð¼Ñƒ Ð²Ñ–Ð´Ð¿Ñ€Ð°Ð²Ð¸Ñ‚Ð¸
Type          VARCHAR(64)  -- follow_up | deadline | overdue | reminder | digest
Payload       JSONB        -- {message, project_id, client_name, ...}
Status        VARCHAR(32)  -- pending | sent | failed | skipped
SendAfter     TIMESTAMPTZ
SentAt        TIMESTAMPTZ  NULLABLE
AttemptCount  INT DEFAULT 0
LastError     VARCHAR(2000) NULLABLE
CreatedAt     TIMESTAMPTZ
```

**Index:** (Status, SendAfter) â€” Ð´Ð»Ñ ReminderWorker

#### price_items (cache Ð· Notion)
```sql
Id            UUID PK
TenantId      UUID
Category      VARCHAR(64)
Title         VARCHAR(256)
PriceUah      JSONB        -- {min, max}
PriceUsd      JSONB        -- {min, max}
PriceEur      JSONB        -- {min, max}
Includes      TEXT
DependsOn     TEXT
NotionPageId  VARCHAR(128)
SyncedAt      TIMESTAMPTZ
```

#### portfolio_cases (cache Ð· Notion)
```sql
Id            UUID PK
TenantId      UUID
Category      VARCHAR(64)  -- logo | identity | social | packaging | print
Title         VARCHAR(256)
Description   TEXT
Niche         VARCHAR(128)
PreviewUrl    TEXT
ExternalUrl   TEXT         -- Behance link
NotionPageId  VARCHAR(128)
SortOrder     INT
SyncedAt      TIMESTAMPTZ
```

---

## ÐšÐ¾Ð½Ñ„Ñ–Ð³ÑƒÑ€Ð°Ñ†Ñ–Ñ (appsettings.json)

```json
{
  "Telegram": {
    "Enabled": true,
    "BotToken": "ENV:TELEGRAM__BOTTOKEN",
    "WebhookSecret": "ENV:TELEGRAM__WEBHOOKSECRET",
    "AdminUserId": "ENV:TELEGRAM__ADMINUSERID",
    "BotUsername": "ENV:TELEGRAM__BOTUSERNAME",
    "MiniAppSettingsUrl": "ENV:TELEGRAM__MINIAPPSETTINGSURL",
    "MiniAppSettingsDirectUrl": "ENV:TELEGRAM__MINIAPPSETTINGSDIRECTURL"
  },
  "Claude": {
    "ApiKey": "ENV:CLAUDE__APIKEY",
    "Model": "claude-sonnet-4-20250514",
    "MaxTokens": 2048
  },
  "OpenAi": {
    "ApiKey": "ENV:OPENAI__APIKEY",
    "Model": "gpt-4o"
  },
  "Notion": {
    "ApiKey": "ENV:NOTION__APIKEY",
    "ApiBaseUrl": "https://api.notion.com/v1",
    "Version": "2022-06-28",
    "LeadsDatabaseId": "ENV:NOTION__LEADSDATABASEID",
    "ProjectsDatabaseId": "ENV:NOTION__PROJECTSDATABASEID",
    "BriefsDatabaseId": "ENV:NOTION__BRIEFSDATABASEID",
    "PriceDatabaseId": "ENV:NOTION__PRICEDATABASEID",
    "PortfolioDatabaseId": "ENV:NOTION__PORTFOLIODATABASEID"
  },
  "GoogleDrive": {
    "CredentialsPath": "ENV:GOOGLEDRIVE__CREDENTIALSPATH",
    "RootFolderId": "ENV:GOOGLEDRIVE__ROOTFOLDERID"
  },
  "GoogleCalendar": {
    "CredentialsPath": "ENV:GOOGLECALENDAR__CREDENTIALSPATH",
    "CalendarId": "ENV:GOOGLECALENDAR__CALENDARID"
  },
  "ConnectionStrings": {
    "DefaultConnection": "ENV:CONNECTIONSTRINGS__DEFAULTCONNECTION"
  },
  "BaguetteDesign": {
    "TenantId": "ENV:BAGUETTEDESIGN__TENANTID",
    "DefaultLocale": "uk"
  },
  "ReminderWorker": {
    "Enabled": true,
    "IntervalSeconds": 300,
    "BatchSize": 20,
    "RunOnStartup": true,
    "MaxBackoffSeconds": 600,
    "BackoffFactor": 2
  }
}
```

### Telegram Mini App Settings Launch

- The main `Settings` reply-keyboard button must stay a plain Telegram button.
- Do not rely on reply-keyboard `web_app` launch for fullscreen behavior.
- Preferred flow:
  1. user taps `Settings`
  2. bot sends a launch-entry message
  3. launch-entry uses `Telegram__MiniAppSettingsDirectUrl` if configured
  4. otherwise it falls back to `https://t.me/<bot>?startapp=settings&mode=compact` derived from `Telegram__BotUsername`
  5. legacy inline settings remain available only as a fallback
- Bootstrap for `/miniapp/settings` should come from `POST /api/session/bootstrap` so the screen can render without extra blocking round-trips for locale, AI provider, or integration status.

---

## Railway Dockerfile

```dockerfile
# src/bots/BaguetteDesign/BaguetteDesign.Api/Dockerfile

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# ÐšÐ¾Ð¿Ñ–ÑŽÑ”Ð¼Ð¾ Ð²ÐµÑÑŒ src/ â€” Ð¿Ð¾Ñ‚Ñ€Ñ–Ð±ÐµÐ½ SharedBotKernel
COPY src/ ./src/

# Ð’Ñ–Ð´Ð½Ð¾Ð²Ð»ÑŽÑ”Ð¼Ð¾ Ð·Ð°Ð»ÐµÐ¶Ð½Ð¾ÑÑ‚Ñ– Ñ‚Ñ–Ð»ÑŒÐºÐ¸ Ð´Ð»Ñ Ñ†ÑŒÐ¾Ð³Ð¾ Ð±Ð¾Ñ‚Ð°
RUN dotnet restore src/bots/BaguetteDesign/BaguetteDesign.Api/BaguetteDesign.Api.csproj

# ÐŸÑƒÐ±Ð»Ñ–ÐºÑƒÑ”Ð¼Ð¾ Ñ‚Ñ–Ð»ÑŒÐºÐ¸ BaguetteDesign
RUN dotnet publish src/bots/BaguetteDesign/BaguetteDesign.Api/BaguetteDesign.Api.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "BaguetteDesign.Api.dll"]
```

---

## Dependency Registration (Program.cs)

```csharp
// BaguetteDesign.Api/Program.cs

var builder = WebApplication.CreateBuilder(args);

// Shared Kernel: Claude, OpenAI, Notion base, Graph, IClock, Telegram dedup
builder.Services.AddKernelServices(builder.Configuration);

// BaguetteDesign specific
builder.Services.AddBaguetteServices(builder.Configuration);
// Ð ÐµÑ”ÑÑ‚Ñ€ÑƒÑ”: BaguetteDbContext, GoogleDriveClient, GoogleCalendarClient,
//           NotionBriefClient, NotionPriceClient, ReminderWorker,
//           BriefFlowService, InboxService, LeadService, ProjectService,
//           FileService, PriceService, PortfolioService, CalendarService,
//           NotificationService, AiAssistantService, RoleRouter

builder.Services.AddControllers();

var app = builder.Build();

// ÐÐ²Ñ‚Ð¾-Ð¼Ñ–Ð³Ñ€Ð°Ñ†Ñ–Ñ Ð¿Ñ€Ð¸ ÑÑ‚Ð°Ñ€Ñ‚Ñ–
await app.Services.GetRequiredService<BaguetteDbContext>().Database.MigrateAsync();

// Ð ÐµÑ”ÑÑ‚Ñ€Ð°Ñ†Ñ–Ñ Telegram Webhook
app.MapPost("/api/telegram/webhook", async (TelegramController ctrl, Update update)
    => await ctrl.Handle(update));

app.MapGet("/health", () => new { status = "healthy" });

app.Run();
```

