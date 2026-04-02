# Архітектура: BotPlatform

> Версія: 1.1 | Статус: Оновлено (SharedBotKernel реалізовано)
> Читати разом з: docs/adr/*.md

---

## Monorepo структура (BotPlatform.sln)

```
BotPlatform.sln
│
├── src/SharedBotKernel/          ← Спільна бібліотека (реалізовано)
│   ├── Domain/                   ← Entities, Base classes, AI types
│   ├── Infrastructure/AI/        ← ClaudeChatClient, OpenAiChatClient
│   └── Persistence/              ← KernelDbContext (abstract)
│
├── src/LagerthaAssistant.*/      ← Побутовий бот (production)
│   ├── LagerthaAssistant.Domain
│   ├── LagerthaAssistant.Application
│   ├── LagerthaAssistant.Infrastructure  ← AppDbContext : KernelDbContext
│   └── LagerthaAssistant.Api
│
└── src/BaguetteDesign.*/         ← Бот для дизайнера (M1+)
    ├── BaguetteDesign.Domain
    ├── BaguetteDesign.Application
    ├── BaguetteDesign.Infrastructure     ← BaguetteDbContext : KernelDbContext
    └── BaguetteDesign.Api
```

---

## C4 Level 1: System Context (BaguetteDesign)

```
[Клієнт (Telegram)]  →  [BaguetteDesign Bot]  →  [Notion]
[Дизайнер (Telegram)] →  [BaguetteDesign Bot]  →  [Google Drive]
                                                →  [Google Calendar]
                                                →  [Claude AI (Anthropic)]
                                                →  [PostgreSQL]
```

---

## C4 Level 2: Container Diagram

```
BaguetteDesign (Railway.app)
│
├── BaguetteDesign.Api           ← ASP.NET Core 10, Webhook endpoint
│   POST /api/telegram/webhook
│   GET  /health
│
├── BaguetteDesign.Application   ← Use-case services
│   BriefFlowService             ← Покроковий бриф
│   InboxService                 ← Inbox дизайнера
│   LeadService                  ← CRM
│   ProjectService               ← Проєкти + статуси
│   FileService                  ← Матеріали клієнтів
│   PriceService                 ← Прайс з Notion
│   PortfolioService             ← Портфоліо з Notion
│   CalendarService              ← Дзвінки
│   NotificationService          ← Розумні нагадування
│   AiAssistantService           ← AI для дизайнера
│
├── BaguetteDesign.Infrastructure ← Adapters
│   BaguetteDbContext             ← EF Core → PostgreSQL
│   GoogleDriveClient             ← Google Drive API
│   GoogleCalendarClient          ← Google Calendar API
│   NotionBriefClient             ← Notion (briefs, leads, projects)
│   NotionPriceClient             ← Notion (price + portfolio cache)
│   ReminderWorker                ← BackgroundSyncWorkerBase<Notification>
│
└── BaguetteDesign.Domain        ← Entities, Value Objects, Interfaces
    Client, Lead, Brief, Project
    Message, FileRecord, CalendarEvent
    Notification, Tenant
    PriceItem, PortfolioCase

SharedBotKernel (Project Reference)
│
├── Domain
│   ConversationSession, ConversationHistoryEntry
│   UserMemoryEntry, SystemPromptEntry, SystemPromptProposal
│   ConversationIntentMetric, TelegramProcessedUpdate
│
├── Infrastructure
│   ClaudeChatClient, OpenAiChatClient, ResolvingAiChatClient
│   NotionHttpClient (базовий HTTP wrapper)
│   GraphHttpClient, TokenManager, GraphAuthService
│   TelegramWebhookAdapter, TelegramDeduplicationService
│   IClock, SystemClock
│
├── Persistence
│   KernelDbContext (abstract)
│
└── Workers
    BackgroundSyncWorkerBase<TJob>

External Services
├── PostgreSQL (Railway) — спільний для обох ботів
├── Anthropic Claude API
├── Notion API (v2022-06-28)
├── Google Drive API v3
└── Google Calendar API v3
```

---

## Telegram Message Processing Flow

```
Telegram → POST /api/telegram/webhook
  ↓
TelegramWebhookAdapter (SharedBotKernel)
  ↓
TelegramDeduplicationService → TelegramProcessedUpdates
  ↓
RoleRouter.Resolve(userId) → Designer | Client
  ↓
  ├── Designer → DesignerUpdateRouter
  │     ├── InboxController
  │     ├── LeadsController
  │     ├── ProjectsController
  │     └── QuickActionController
  │
  └── Client → ClientUpdateRouter
        ├── StartHandler
        ├── QuestionHandler (Claude)
        ├── BriefFlowHandler (state machine)
        ├── PriceHandler (Notion)
        ├── PortfolioHandler (Notion)
        ├── ContactHandler (Calendar)
        └── StatusHandler
```

---

## База даних: BaguetteDbContext

### Таблиці SharedBotKernel (успадковуються через KernelDbContext)

| Таблиця | Призначення |
|---|---|
| ConversationSessions | Одна сесія на scope (channel+userId+conversationId) |
| ConversationHistoryEntries | Повна переписка сесії |
| UserMemoryEntries | Key/Value пам'ять з Confidence |
| SystemPromptEntries | Версіонований system prompt Claude |
| SystemPromptProposals | Пропозиції змін промпту |
| ConversationIntentMetrics | Аналітика по intent/agent |
| TelegramProcessedUpdates | Дедуплікація webhook (UpdateId PK) |

### Таблиці BaguetteDesign-specific

#### tenants
```sql
Id          UUID PK
OwnerId     UUID FK → users (Telegram designer)
Name        VARCHAR(256)      -- "Студія Lesia Design"
InviteRef   VARCHAR(64) UNIQUE -- для t.me/bot?start=ref_xxx
CreatedAt   TIMESTAMPTZ
```

#### designer_clients
```sql
Id          UUID PK
TenantId    UUID              -- ЗАВЖДИ присутній (ADR-004)
TgId        BIGINT            -- Telegram user_id
Name        VARCHAR(256)
Contact     VARCHAR(256)
Country     VARCHAR(64)
Tags        TEXT[]            -- VIP, повторний, холодний
Source      VARCHAR(64)       -- telegram | referral | manual
CreatedAt   TIMESTAMPTZ
UpdatedAt   TIMESTAMPTZ
```

#### leads
```sql
Id           UUID PK
TenantId     UUID
ClientId     UUID FK → designer_clients
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
LeadId          UUID FK → leads
ClientId        UUID FK → designer_clients
RawDialog       JSONB         -- весь діалог брифу [{role, content, timestamp}]
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
ClientId        UUID FK → designer_clients
LeadId          UUID FK → leads  NULLABLE
BriefId         UUID FK → briefs NULLABLE
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
ClientId      UUID FK → designer_clients
ProjectId     UUID FK → projects NULLABLE
Role          VARCHAR(32)   -- client | bot | designer
Content       TEXT
TgMessageId   BIGINT        -- для reply/forward
CreatedAt     TIMESTAMPTZ
```

**Index:** (TenantId, ClientId, CreatedAt DESC)

#### file_records
```sql
Id              UUID PK
TenantId        UUID
ProjectId       UUID FK → projects NULLABLE
ClientId        UUID FK → designer_clients
FileType        VARCHAR(64)  -- logo | reference | brandbook | text | final
OriginalName    VARCHAR(256)
DriveFileId     VARCHAR(256) -- Google Drive file ID
TgFileId        VARCHAR(256) -- Telegram file_id для download
Url             TEXT         -- прямий Google Drive link
UploadedAt      TIMESTAMPTZ
```

#### calendar_events
```sql
Id              UUID PK
TenantId        UUID
ClientId        UUID FK → designer_clients
ProjectId       UUID FK → projects NULLABLE
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
TargetTgId    BIGINT       -- кому відправити
Type          VARCHAR(64)  -- follow_up | deadline | overdue | reminder | digest
Payload       JSONB        -- {message, project_id, client_name, ...}
Status        VARCHAR(32)  -- pending | sent | failed | skipped
SendAfter     TIMESTAMPTZ
SentAt        TIMESTAMPTZ  NULLABLE
AttemptCount  INT DEFAULT 0
LastError     VARCHAR(2000) NULLABLE
CreatedAt     TIMESTAMPTZ
```

**Index:** (Status, SendAfter) — для ReminderWorker

#### price_items (cache з Notion)
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

#### portfolio_cases (cache з Notion)
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

## Конфігурація (appsettings.json)

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
  4. otherwise it falls back to `https://t.me/<bot>?startapp=settings` derived from `Telegram__BotUsername`
  5. legacy inline settings remain available only as a fallback
- Bootstrap for `/miniapp/settings` should come from `POST /api/session/bootstrap` so the screen can render without extra blocking round-trips for locale, AI provider, or integration status.

---

## Railway Dockerfile

```dockerfile
# src/bots/BaguetteDesign/BaguetteDesign.Api/Dockerfile

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

# Копіюємо весь src/ — потрібен SharedBotKernel
COPY src/ ./src/

# Відновлюємо залежності тільки для цього бота
RUN dotnet restore src/bots/BaguetteDesign/BaguetteDesign.Api/BaguetteDesign.Api.csproj

# Публікуємо тільки BaguetteDesign
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
// Реєструє: BaguetteDbContext, GoogleDriveClient, GoogleCalendarClient,
//           NotionBriefClient, NotionPriceClient, ReminderWorker,
//           BriefFlowService, InboxService, LeadService, ProjectService,
//           FileService, PriceService, PortfolioService, CalendarService,
//           NotificationService, AiAssistantService, RoleRouter

builder.Services.AddControllers();

var app = builder.Build();

// Авто-міграція при старті
await app.Services.GetRequiredService<BaguetteDbContext>().Database.MigrateAsync();

// Реєстрація Telegram Webhook
app.MapPost("/api/telegram/webhook", async (TelegramController ctrl, Update update)
    => await ctrl.Handle(update));

app.MapGet("/health", () => new { status = "healthy" });

app.Run();
```
