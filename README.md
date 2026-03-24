# LagerthaAssistant

Console and Telegram AI assistant built with Clean Architecture and PostgreSQL persistence.

## Projects

- `src/LagerthaAssistant.Domain` - domain rules, entities, shared abstractions.
- `src/LagerthaAssistant.Application` - use-case services, interfaces, prompt/memory/vocabulary parsing logic.
- `src/LagerthaAssistant.Infrastructure` - EF Core, repositories, migrations, OpenAI HTTP client, local Excel + OneDrive Graph deck integration, Notion adapters (vocabulary + food sync).
- `src/LagerthaAssistant.Api` - ASP.NET Core Web API with Telegram webhook adapter and background sync workers.
- `src/LagerthaAssistant.UI` - interactive console app and command routing.
- `tests/LagerthaAssistant.*` - domain, application, and integration tests.

## Core capabilities

- Persistent conversation history (`ConversationSessions`, `ConversationHistoryEntries`).
- Persistent user memory (`UserMemoryEntries`) injected into next requests.
- Persistent versioned system prompts (`SystemPromptEntries`).
- Prompt proposal workflow (`SystemPromptProposals`) with apply/reject flow.
- Conversation telemetry metrics (`ConversationIntentMetrics`) for channel/agent/intent analysis.
- Vocabulary workflow with Excel (`.xlsx`) decks in two storage modes:
  - `local` (direct filesystem access)
  - `graph` (OneDrive via Microsoft Graph)
- Smart import sources for vocabulary:
  - URL page text
  - raw text
  - photo OCR
  - file upload (`.txt`, `.pdf`, `.xlsx`, `.docx`)
- Telegram chat mode with natural-language action routing (assistant can trigger supported bot flows without slash commands).
- Vocabulary cache maintenance actions (`rebuild` / `clear`) for fast duplicate lookup hygiene.
- Telegram channel adapter via webhook (`POST /api/telegram/webhook`).
- Food tracking domain model in SQL:
  - inventory items
  - meal plans + ingredients
  - grocery list
  - meal history + calorie/macros summaries
- Notion Food integration (3 databases) with bidirectional sync:
  - Notion -> SQL (inventory/meals/grocery pull)
  - SQL -> Notion (grocery status push)
  - retry/backoff worker in API host
- Agent orchestration boundaries:
  - command intent is resolved once in orchestrator and reused by agents,
  - boundary policy blocks vocabulary agent from slash commands and command intents,
  - command agent is marked as non-batch to keep tool responsibilities isolated.

## Database schema

PostgreSQL (Npgsql). All tables use auto-increment integer primary keys unless noted. Timestamps are stored in UTC.

### ConversationSessions

Stores one record per logical conversation scope (channel + user + conversationId).

| Column | Type | Notes |
|---|---|---|
| Id | int | PK |
| SessionKey | guid | Unique. Stable identifier for the session. |
| Channel | varchar(64) | Source channel: `ui`, `api`, `telegram`. |
| UserId | varchar(128) | User identity within the channel. |
| ConversationId | varchar(128) | Conversation scope within the user. |
| Title | varchar(200) | Optional display title. |
| CreatedAt / UpdatedAt | datetime | Audit timestamps (UTC). |

Indexes: unique on `SessionKey`; composite on `(Channel, UserId, ConversationId, UpdatedAt)`.

### ConversationHistoryEntries

Message log for a session. Deleted automatically when the parent session is deleted.

| Column | Type | Notes |
|---|---|---|
| Id | int | PK |
| ConversationSessionId | int | FK → ConversationSessions (cascade delete). |
| Role | varchar(32) | Enum stored as string: `System`, `User`, `Assistant`. |
| Content | varchar(8000) | Raw message text. |
| SentAtUtc | datetimeoffset | When the message was sent. |
| CreatedAt / UpdatedAt | datetime | Audit timestamps (UTC). |

Index: composite on `(ConversationSessionId, SentAtUtc)`.

### UserMemoryEntries

Key/value facts learned per user. Injected into context on the next request.

| Column | Type | Notes |
|---|---|---|
| Id | int | PK |
| Channel | varchar(64) | |
| UserId | varchar(128) | |
| Key | varchar(128) | Fact name, e.g. `native_language`. |
| Value | varchar(2000) | Fact value. |
| Confidence | double | 0.0–1.0 confidence score. |
| IsActive | bool | Inactive entries are excluded from context. |
| LastSeenAtUtc | datetimeoffset | Last time this fact was confirmed. |
| CreatedAt / UpdatedAt | datetime | Audit timestamps (UTC). |

Indexes: unique on `(Channel, UserId, Key)`; composite on `(Channel, UserId, IsActive, UpdatedAt)`.

### SystemPromptEntries

Versioned system prompts. Only one entry can be active at a time.

| Column | Type | Notes |
|---|---|---|
| Id | int | PK |
| PromptText | varchar(8000) | Full prompt text. |
| Version | int | Unique version number, auto-incremented on each change. |
| IsActive | bool | Unique filtered index ensures at most one active row. |
| Source | varchar(64) | Who created the version: `default`, `manual`, `applied-proposal`, etc. |
| CreatedAtUtc | datetimeoffset | When this version was created. |
| CreatedAt / UpdatedAt | datetime | Audit timestamps (UTC). |

Indexes: unique on `Version`; unique filtered index on `IsActive WHERE IsActive = TRUE`.

### SystemPromptProposals

Proposed prompt changes, pending user review.

| Column | Type | Notes |
|---|---|---|
| Id | int | PK |
| ProposedPrompt | varchar(8000) | The proposed text. |
| Reason | varchar(1000) | Why the change is proposed. |
| Confidence | double | Model confidence in the proposal. |
| Source | varchar(64) | Origin: `ai`, `manual`, etc. |
| Status | varchar(32) | `pending`, `applied`, `rejected`. |
| CreatedAtUtc | datetimeoffset | |
| ReviewedAtUtc | datetimeoffset | Nullable. Set when applied or rejected. |
| AppliedSystemPromptEntryId | int | Nullable. Points to the SystemPromptEntry created on apply. |
| CreatedAt / UpdatedAt | datetime | Audit timestamps (UTC). |

Index: composite on `(Status, CreatedAtUtc)`.

### VocabularyCards

SQL index cache of vocabulary entries read from Excel decks. Used for fast duplicate detection before scanning files.

| Column | Type | Notes |
|---|---|---|
| Id | int | PK |
| Word | varchar(512) | Original word as stored in the deck. |
| NormalizedWord | varchar(512) | Lowercased, trimmed form used for lookups. |
| Meaning | varchar(8000) | Full meaning cell content from Excel. |
| Examples | varchar(8000) | Example sentences from Excel. |
| PartOfSpeechMarker | varchar(32) | Nullable. Extracted marker: `n`, `v`, `adj`, etc. |
| DeckFileName | varchar(256) | Name of the source `.xlsx` file. |
| DeckPath | varchar(1000) | Full path or OneDrive path to the deck. |
| LastKnownRowNumber | int | Row number in Excel where the entry was last seen. |
| StorageMode | varchar(32) | `local` or `graph`. |
| SyncStatus | varchar(32) | Enum: `Synced`, `Pending`, `Failed`. Tracks whether the card is written back to Excel. |
| LastSyncError | varchar(2000) | Nullable. Last Excel write error message. |
| FirstSeenAtUtc | datetimeoffset | When the card was first cached. |
| LastSeenAtUtc | datetimeoffset | Last time the card was found during a lookup or rebuild. |
| SyncedAtUtc | datetimeoffset | Nullable. Last successful Excel sync. |
| NotionSyncStatus | varchar(32) | Enum: `Pending`, `Processing`, `Synced`, `Failed`. Tracks Notion export. |
| NotionPageId | varchar(128) | Nullable. Notion page ID after successful export. |
| NotionAttemptCount | int | Number of Notion export attempts. |
| NotionLastError | varchar(2000) | Nullable. Last Notion export error. |
| NotionLastAttemptAtUtc | datetimeoffset | Nullable. |
| NotionSyncedAtUtc | datetimeoffset | Nullable. Last successful Notion export. |
| CreatedAt / UpdatedAt | datetime | Audit timestamps (UTC). |

Indexes: unique on `(NormalizedWord, DeckFileName, StorageMode)`; composite on `(StorageMode, LastSeenAtUtc)`; composite on `(SyncStatus, UpdatedAt)`; composite on `(NotionSyncStatus, NotionLastAttemptAtUtc)`.

### VocabularyCardTokens

Individual word tokens extracted from `VocabularyCard.Word` for fast multi-token lookup.

| Column | Type | Notes |
|---|---|---|
| Id | int | PK |
| VocabularyCardId | int | FK → VocabularyCards (cascade delete). |
| TokenNormalized | varchar(256) | Lowercased individual token. |

Indexes: non-unique on `TokenNormalized`; unique composite on `(VocabularyCardId, TokenNormalized)`.

### VocabularySyncJobs

Persistent queue for failed or deferred Excel write operations. Ensures no data is lost if the file is locked or unavailable at save time.

| Column | Type | Notes |
|---|---|---|
| Id | int | PK |
| RequestedWord | varchar(512) | The word the user requested. |
| AssistantReply | varchar(8000) | Full AI response to parse and write. |
| TargetDeckFileName | varchar(256) | Target `.xlsx` file name. |
| TargetDeckPath | varchar(1000) | Nullable. Resolved full path. |
| OverridePartOfSpeech | varchar(32) | Nullable. User-specified POS override. |
| StorageMode | varchar(32) | `local` or `graph`. |
| Status | varchar(32) | Enum: `Pending`, `Processing`, `Completed`, `Failed`. |
| AttemptCount | int | Total number of processing attempts. |
| LastError | varchar(2000) | Nullable. Last failure reason. |
| CreatedAtUtc | datetimeoffset | When the job was created. |
| LastAttemptAtUtc | datetimeoffset | Nullable. |
| CompletedAtUtc | datetimeoffset | Nullable. Set on success. |
| CreatedAt / UpdatedAt | datetime | Audit timestamps (UTC). |

Index: composite on `(Status, CreatedAtUtc)`.

### ConversationIntentMetrics

Daily aggregated counters for channel/agent/intent combinations. Used by the telemetry API.

| Column | Type | Notes |
|---|---|---|
| Id | int | PK |
| MetricDateUtc | date | Date portion only (no time). |
| Channel | varchar(64) | |
| AgentName | varchar(128) | Which agent handled the request. |
| Intent | varchar(128) | Detected intent name. |
| IsBatch | bool | Whether it was a batch request. |
| Count | int | Number of requests in this bucket. |
| TotalItems | int | Total vocabulary items processed (batch). |
| LastSeenAtUtc | datetimeoffset | Last time this bucket was incremented. |
| CreatedAt / UpdatedAt | datetime | Audit timestamps (UTC). |

Indexes: unique on `(MetricDateUtc, Channel, AgentName, Intent, IsBatch)`; composite on `(MetricDateUtc, Channel, Count)`.

### TelegramProcessedUpdates

Deduplication table for Telegram webhook updates. Prevents double-processing when Telegram retries delivery.

| Column | Type | Notes |
|---|---|---|
| UpdateId | long | PK (not auto-generated; value comes from Telegram). |
| ProcessedAtUtc | datetimeoffset | When the update was first processed. |

Index: non-unique on `ProcessedAtUtc`.

---

## Vocabulary workflow

When you type a word or phrase (non-command input):

1. Assistant checks duplicates in writable decks for the active storage mode.
2. If found, it prints saved data and skips AI call.
3. If not found, it calls AI and parses the response.
4. Before write, app decides by save mode: `ask` (confirm), `auto` (write immediately), `off` (skip writing).

Batch smart-paste mode:

- Run `/batch`.
- Paste multiple items and finish with `/end` (or cancel with `/cancel`).
- Parser auto-detects entries by line, tab, `;`, and sentence boundaries (`.`, `!`, `?`) for single-line paste.
- For one-line space-separated text without separators, app can ask whether to keep one phrase or split by spaces.
- Items are processed sequentially.
- Duplicate items inside one batch are computed once and reused for repeated entries in the same request/session step.
- For unresolved inputs, writable-deck duplicate lookup runs in one batch pass (single deck scan) in both `local` and `graph` storage modes.
- In `ask` mode app prompts once at the end to save all, review targets, or skip.

Irregular verbs are supported via a dedicated deck (for example `beat - beat - beaten`). Duplicate lookup matches by any form (base/past/participle).

Excel columns used:

- `A` - meanings (`(n) ...`, `(v) ...`) with line breaks.
- `B` - English word.
- `H` - example sentences with line breaks.

Read-only (composite) decks are configured via `VocabularyDecks.ReadOnlyFileNames` and are never written.

SQL index + sync queue:

- App stores indexed vocabulary records in PostgreSQL (`VocabularyCards`, `VocabularyCardTokens`) for fast duplicate lookup before Excel scan.
- Excel/OneDrive remains the final export source for your mobile app workflow.
- If write to Excel fails due lock/conflict, app stores a pending sync job in PostgreSQL (`VocabularySyncJobs`) so data is not lost and can be retried by worker flow.
- Pending sync jobs are deduplicated by payload for active states (`pending`/`processing`) to reduce queue spam from repeated save attempts.
- Sync processors claim pending jobs before execution to avoid double-processing when UI/API/manual runs overlap.
- Recoverable failures are retried up to 8 attempts; after the limit the job is marked `failed` with terminal error details.

## Configuration

Set OpenAI API key (required):

```powershell
$env:OPENAI_API_KEY = "your_api_key"
```

Connection string and deck settings are configured in:

- `src/LagerthaAssistant.UI/appsettings.json`
- `src/LagerthaAssistant.Api/appsettings.json`

### PostgreSQL connection string

Local development:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Database=LagerthaAssistantDb;Username=postgres;Password=your_password"
}
```

Railway.app (use environment variable `ConnectionStrings__DefaultConnection`):

```
Host=<host>.railway.app;Port=5432;Database=railway;Username=postgres;Password=<password>
```

### Storage mode

```json
"VocabularyStorage": {
  "DefaultMode": "local"
}
```

Values:
- `local` - use local files from `VocabularyDecks.FolderPath`
- `graph` - use OneDrive via Graph

### Graph settings

```json
"Graph": {
  "TenantId": "common",
  "ClientId": "<your app client id>",
  "Scopes": ["User.Read", "Files.ReadWrite", "offline_access"],
  "RootPath": "/Apps/Flashcards Deluxe",
  "TokenCachePath": "%LOCALAPPDATA%\\LagerthaAssistant\\graph-token.json"
}
```

Notes:
- `ClientId` is required for Graph mode/login.
- App uses device-code login (`/graph login`).
- Token cache is stored locally at `TokenCachePath`.
- You usually do **not** need `/graph login` on every app start. After first successful sign-in, cached token/refresh token are reused automatically.
- Run `/graph login` again only if `/graph status` says `Not authenticated` (or after `/graph logout`).
- Open the exact sign-in URL printed by the app (in some tenants it is `https://www.microsoft.com/link`).

### Telegram settings (API channel adapter)

```json
"Telegram": {
  "Enabled": false,
  "BotToken": "<telegram bot token>",
  "ApiBaseUrl": "https://api.telegram.org",
  "WebhookSecret": "<optional secret token>"
}
```

Notes:
- Integration entrypoint: `POST /api/telegram/webhook`.
- Session/state mapping:
  - `channel = telegram`
  - `userId = message.from.id`
  - `conversationId = chat.id` (or `chat.id:message_thread_id` for topic threads)
- If `WebhookSecret` is set, requests must include header `X-Telegram-Bot-Api-Secret-Token`.
- Telegram navigation layer:
  - persistent Reply Keyboard on `/start` and when returning to main menu,
  - inline section actions for vocabulary/shopping/weekly-menu flows,
  - callback routing by `callback_data` prefixes: `nav:`, `vocab:`, `shop:`, `weekly:`.
- Locale behavior:
  - supported locales: `en`, `uk` (Russian is never used),
  - first locale is initialized from Telegram `language_code` (`ru*` is forced to `uk`),
  - locale is persisted in `UserMemoryEntries` under key `locale`,
  - auto-switch between `en` and `uk` uses consecutive-message confidence tracking.

### Notion settings (SQL-first export adapter)

```json
"Notion": {
  "Enabled": false,
  "ApiKey": "<notion integration secret>",
  "DatabaseId": "<notion database id>",
  "ApiBaseUrl": "https://api.notion.com/v1",
  "Version": "2022-06-28",
  "ConflictMode": "update",
  "RequestTimeoutSeconds": 60,
  "KeyPropertyName": "Key",
  "WordPropertyName": "Word",
  "MeaningPropertyName": "Meaning",
  "ExamplesPropertyName": "Examples",
  "PartOfSpeechPropertyName": "PartOfSpeech",
  "DeckPropertyName": "DeckFile",
  "StorageModePropertyName": "StorageMode",
  "RowNumberPropertyName": "RowNumber",
  "LastSeenPropertyName": "LastSeenAtUtc"
}
```

Notes:
- Export source is SQL index (`VocabularyCards`), not direct Excel file reads.
- Conflict handling is controlled by `Notion:ConflictMode`:
  - `update` - update existing page with same key (`normalizedWord|deck|storageMode`)
  - `skip` - keep existing page and mark sync as completed
  - `error` - treat existing page as conflict error
- Retries use persistent SQL card state (`Pending`/`Processing`/`Synced`/`Failed`) with capped recoverable attempts.

### Notion sync worker (API only)

```json
"NotionSyncWorker": {
  "Enabled": false,
  "IntervalSeconds": 60,
  "BatchSize": 25,
  "RunOnStartup": true,
  "MaxBackoffSeconds": 300,
  "BackoffFactor": 2
}
```

### Notion food settings (Inventory / Meal Plans / Grocery List)

```json
"NotionFood": {
  "Enabled": false,
  "ApiKey": "<notion integration secret>",
  "InventoryDatabaseId": "<notion inventory db id>",
  "MealPlansDatabaseId": "<notion meal plans db id>",
  "GroceryListDatabaseId": "<notion grocery list db id>",
  "ApiBaseUrl": "https://api.notion.com/v1",
  "Version": "2022-06-28",
  "RequestTimeoutSeconds": 60
}
```

Notes:
- Use the same Notion integration token that has access to all 3 databases.
- For production (Railway), keep all secrets/IDs in environment variables, not in repo files.
- If `Enabled=false` or any required value is missing, food sync calls are blocked intentionally.

### Food sync worker (API only)

```json
"FoodSyncWorker": {
  "Enabled": false,
  "SyncFromNotionIntervalSeconds": 300,
  "SyncToNotionIntervalSeconds": 30,
  "BatchSize": 25,
  "RunOnStartup": true,
  "MaxBackoffSeconds": 600,
  "BackoffFactor": 2
}
```

Notes:
- Runs only in API host.
- `SyncFromNotionIntervalSeconds`: pull Inventory/Meals/Grocery from Notion into SQL.
- `SyncToNotionIntervalSeconds`: push local grocery state updates to Notion.
- Worker applies exponential backoff on repeated pull failures.

### Environment keys reference (Railway)

Set values in Railway service variables (do not keep secrets in repo files).

Required minimum:

```text
ConnectionStrings__DefaultConnection
OpenAI__ApiKey
Telegram__Enabled
Telegram__BotToken
Telegram__WebhookSecret
```

Vocabulary + storage mode:

```text
VocabularyStorage__DefaultMode
VocabularyDecks__FolderPath
VocabularyDecks__FilePattern
VocabularyDecks__IrregularVerbDeckFileName
VocabularyDecks__PhrasalVerbDeckFileName
VocabularyDecks__ReadOnlyFileNames__0
VocabularyDecks__ReadOnlyFileNames__1
VocabularyDecks__ReadOnlyFileNames__2
```

Graph / OneDrive:

```text
Graph__TenantId
Graph__ClientId
Graph__RootPath
Graph__TokenCachePath
```

Vocabulary Notion export:

```text
Notion__Enabled
Notion__ApiKey
Notion__DatabaseId
Notion__ApiBaseUrl
Notion__Version
Notion__ConflictMode
Notion__RequestTimeoutSeconds
NotionSyncWorker__Enabled
NotionSyncWorker__IntervalSeconds
NotionSyncWorker__BatchSize
NotionSyncWorker__RunOnStartup
NotionSyncWorker__MaxBackoffSeconds
NotionSyncWorker__BackoffFactor
```

Food Notion sync (new):

```text
NotionFood__Enabled
NotionFood__ApiKey
NotionFood__InventoryDatabaseId
NotionFood__MealPlansDatabaseId
NotionFood__GroceryListDatabaseId
NotionFood__ApiBaseUrl
NotionFood__Version
NotionFood__RequestTimeoutSeconds
FoodSyncWorker__Enabled
FoodSyncWorker__SyncFromNotionIntervalSeconds
FoodSyncWorker__SyncToNotionIntervalSeconds
FoodSyncWorker__BatchSize
FoodSyncWorker__RunOnStartup
FoodSyncWorker__MaxBackoffSeconds
FoodSyncWorker__BackoffFactor
```

UI scope overrides (optional):

- `LAGERTHA_USER_ID` - override default UI user identity (otherwise OS username is used).
- `LAGERTHA_CONVERSATION_ID` - override default UI conversation id (`main`).

### Background sync worker (API only)

```json
"VocabularySyncWorker": {
  "Enabled": false,
  "IntervalSeconds": 60,
  "BatchSize": 25,
  "RunOnStartup": true,
  "MaxBackoffSeconds": 300,
  "BackoffFactor": 2
}
```

Notes:
- Worker runs only in API host (`LagerthaAssistant.Api`), not in console UI.
- Keep `Enabled=false` by default; turn on when you want automatic retry of pending sync jobs.
- Worker applies exponential backoff on runtime failures up to `MaxBackoffSeconds` (`BackoffFactor` controls growth).
- Manual processing is available from UI commands (`/sync`, `/sync run`) and API endpoints.
- Worker/manual runs use the same queue claim logic, so concurrent runs do not process the same pending job twice.

### Microsoft Entra quick setup

1. Open [Microsoft Entra admin center](https://entra.microsoft.com/) -> `Identity` -> `Applications` -> `App registrations` -> `New registration`.
2. Create app and set `Supported account types` to `Accounts in any organizational directory and personal Microsoft accounts`.
3. Open created app -> `Authentication` and enable `Allow public client flows` = `Yes`.
4. Open `API permissions` -> `Add a permission` -> `Microsoft Graph` -> `Delegated permissions`, then add `User.Read`, `Files.ReadWrite`, `offline_access`.
5. Copy `Application (client) ID` from `Overview` and put it into `Graph:ClientId` in `appsettings.json`.
6. Set `Graph:TenantId`:
   - `consumers` for personal OneDrive
   - `common` for work/school or mixed scenarios
7. Start app, run `/graph login`, complete device-code sign-in in browser, then check `/graph status`.

### Deck mapping and file rules

```json
"VocabularyDecks": {
  "FolderPath": "%OneDrive%\\Apps\\Flashcards Deluxe",
  "FilePattern": "wm-*.xlsx",
  "IrregularVerbDeckFileName": "wm-irregular-verbs-ua-en.xlsx",
  "PhrasalVerbDeckFileName": "wm-phrasal-verbs-ua-en.xlsx",
  "ReadOnlyFileNames": [
    "wm-vocabulary-all-ru-en.xlsx",
    "wm-training-all-ru-en.xlsx",
    "wm-grammar-all-ru-en.xlsx"
  ]
}
```

## Local development

### Prerequisites

- .NET 10 SDK
- PostgreSQL 16+ (or any 14+)
- OpenAI API key

### Database setup (first time)

After cloning the repo, apply existing migrations (do not generate new initial migration):

```powershell
dotnet ef database update `
  --project src/LagerthaAssistant.Infrastructure/LagerthaAssistant.Infrastructure.csproj `
  --startup-project src/LagerthaAssistant.Api/LagerthaAssistant.Api.csproj
```

Notes:
- Both UI and API also run `Database.Migrate()` on startup.
- On Railway you normally do not run EF CLI manually.

### Run

Console UI:

```powershell
dotnet run --project src/LagerthaAssistant.UI/LagerthaAssistant.UI.csproj
```

API service:

```powershell
dotnet run --project src/LagerthaAssistant.Api/LagerthaAssistant.Api.csproj
```

Quick checks:

```powershell
curl http://localhost:5000/health
curl -X POST http://localhost:5000/api/conversation/messages -H "Content-Type: application/json" -d "{\"input\":\"void\"}"
curl http://localhost:5000/api/vocabulary-sync/status
curl -X POST "http://localhost:5000/api/vocabulary-sync/run?take=25"
curl "http://localhost:5000/api/vocabulary-sync/failed?take=20"
curl -X POST "http://localhost:5000/api/vocabulary-sync/retry-failed?take=25"
curl http://localhost:5000/api/notion-sync/status
curl -X POST "http://localhost:5000/api/notion-sync/run?take=25"
curl "http://localhost:5000/api/notion-sync/failed?take=20"
curl -X POST "http://localhost:5000/api/notion-sync/retry-failed?take=25"
curl "http://localhost:5000/api/telemetry/intents?days=7&top=20&channel=api"
curl http://localhost:5000/api/conversation/commands
curl http://localhost:5000/api/conversation/commands/grouped
curl "http://localhost:5000/api/conversation/history?take=20&channel=api&userId=anonymous&conversationId=default"
curl "http://localhost:5000/api/conversation/memory?take=20&channel=api&userId=anonymous&conversationId=default"
curl http://localhost:5000/api/conversation/prompt
curl "http://localhost:5000/api/conversation/prompt/history?take=20"
curl "http://localhost:5000/api/conversation/prompt/proposals?take=20"
curl -X PUT http://localhost:5000/api/conversation/prompt -H "Content-Type: application/json" -d "{\"prompt\":\"Keep replies concise\",\"source\":\"manual-api\"}"
curl -X POST http://localhost:5000/api/conversation/prompt/default
curl -X POST http://localhost:5000/api/conversation/prompt/proposals -H "Content-Type: application/json" -d "{\"prompt\":\"Focus on practical outputs\",\"reason\":\"reduce verbosity\"}"
curl -X POST http://localhost:5000/api/conversation/prompt/proposals/improve -H "Content-Type: application/json" -d "{\"goal\":\"make outputs more practical\"}"
curl -X POST http://localhost:5000/api/conversation/prompt/proposals/1/apply
curl -X POST http://localhost:5000/api/conversation/prompt/proposals/1/reject
curl -X POST "http://localhost:5000/api/conversation/reset?channel=api&userId=anonymous&conversationId=default"
curl "http://localhost:5000/api/session/bootstrap?channel=api&userId=anonymous&conversationId=default"
curl "http://localhost:5000/api/session/bootstrap?channel=api&userId=anonymous&conversationId=default&includeDecks=true"
curl "http://localhost:5000/api/session/bootstrap?channel=api&userId=anonymous&conversationId=default&includeCommands=false&includePartOfSpeechOptions=false"
curl http://localhost:5000/api/graph/status
curl -X POST http://localhost:5000/api/graph/login
curl -X POST http://localhost:5000/api/graph/login/start
curl -X POST http://localhost:5000/api/graph/login/complete -H "Content-Type: application/json" -d "{\"challenge\":{\"deviceCode\":\"<device-code>\",\"userCode\":\"<user-code>\",\"verificationUri\":\"https://www.microsoft.com/link\",\"expiresInSeconds\":900,\"intervalSeconds\":5,\"expiresAtUtc\":\"2026-03-20T10:15:00Z\"}}"
curl -X POST http://localhost:5000/api/graph/logout
curl -X POST http://localhost:5000/api/telegram/webhook -H "Content-Type: application/json" -d "{\"update_id\":1,\"message\":{\"message_id\":10,\"from\":{\"id\":2002,\"is_bot\":false,\"first_name\":\"Mike\"},\"chat\":{\"id\":1001,\"type\":\"private\"},\"text\":\"void\"}}"
curl -X POST http://localhost:5000/api/vocabulary/analyze -H "Content-Type: application/json" -d "{\"input\":\"void\",\"channel\":\"api\",\"userId\":\"anonymous\",\"conversationId\":\"default\",\"storageMode\":\"local\"}"
curl -X POST http://localhost:5000/api/vocabulary/analyze-batch -H "Content-Type: application/json" -d "{\"inputs\":[\"void\",\"call back\"],\"channel\":\"api\",\"userId\":\"anonymous\",\"conversationId\":\"default\",\"storageMode\":\"graph\"}"
curl "http://localhost:5000/api/vocabulary/storage-mode?channel=api&userId=anonymous&conversationId=default"
curl -X PUT http://localhost:5000/api/vocabulary/storage-mode -H "Content-Type: application/json" -d "{\"mode\":\"graph\",\"channel\":\"api\",\"userId\":\"anonymous\",\"conversationId\":\"default\"}"
curl "http://localhost:5000/api/preferences/save-mode?channel=api&userId=anonymous&conversationId=default"
curl -X PUT http://localhost:5000/api/preferences/save-mode -H "Content-Type: application/json" -d "{\"mode\":\"auto\",\"channel\":\"api\",\"userId\":\"anonymous\",\"conversationId\":\"default\"}"
curl "http://localhost:5000/api/preferences/session?channel=api&userId=anonymous&conversationId=default"
curl -X PUT http://localhost:5000/api/preferences/session -H "Content-Type: application/json" -d "{\"saveMode\":\"auto\",\"storageMode\":\"graph\",\"channel\":\"api\",\"userId\":\"anonymous\",\"conversationId\":\"default\"}"
curl "http://localhost:5000/api/vocabulary/decks?channel=api&userId=anonymous&conversationId=default"
curl http://localhost:5000/api/vocabulary/markers
curl -X POST http://localhost:5000/api/vocabulary/parse-batch -H "Content-Type: application/json" -d "{\"input\":\"void prepare\",\"applySpaceSplit\":false}"
curl -X POST "http://localhost:5000/api/vocabulary/save?channel=api&userId=anonymous&conversationId=default&storageMode=local" -H "Content-Type: application/json" -d "{\"requestedWord\":\"void\",\"assistantReply\":\"void\\n\\n(n) emptiness\"}"
curl -X POST "http://localhost:5000/api/vocabulary/save-batch?channel=api&userId=anonymous&conversationId=default&storageMode=graph" -H "Content-Type: application/json" -d "{\"items\":[{\"requestedWord\":\"void\",\"assistantReply\":\"void\\n\\n(n) emptiness\"},{\"requestedWord\":\"prepare\",\"assistantReply\":\"prepare\\n\\n(v) to prepare\"}]}"
```

On startup both UI and API apply EF migrations automatically.

### Team workflow rules (important)

This repository follows a strict delivery workflow:

- Work branch: `dev` only. Do not create feature branches.
- Pull requests: only `dev -> master`.
- PR description format: same style as PR #62:
  - `Summary`
  - `Key changes`
  - `Validation / Test Plan`
- Every implementation must include automated tests (unit/integration as applicable).
- Before opening PR:
  - sync `dev` with latest `origin/master`
  - run test suites
- After opening PR:
  - always check mergeability/conflicts
  - if conflicts exist, resolve them immediately in `dev`, push, and re-check PR status until `CLEAN`/`MERGEABLE`
- After conflict resolution:
  - rerun tests before final merge approval.

Practical guardrails:

- Keep project/repo text in English; use localized text only where localization is intended.
- Avoid bulk text-rewrite operations on Unicode-heavy files (localization, Telegram text) without verification.
- If non-UTF artifacts appear (mojibake), fix encoding before commit.

## Deploy to Railway.app

### Step 1 - Create Telegram bot

1. Open Telegram, find `@BotFather`.
2. Send `/newbot`, enter a name and username (must end with `bot`).
3. Save the token: `1234567890:ABCDEFabcdef...`
4. Choose a `WebhookSecret` (any random string), for example `my-secret-42`.

### Step 2 - Create Railway project

1. Go to [railway.app](https://railway.app) and sign in with GitHub.
2. Click **New Project -> Deploy from GitHub repo** and select this repository.
3. In service settings, configure Docker build:
   - **Root Directory**: `src`
   - **Dockerfile Path**: `LagerthaAssistant.Api/Dockerfile`

### Step 3 - Add PostgreSQL

1. In your Railway project click **New -> Database -> Add PostgreSQL**.
2. Open PostgreSQL service -> **Variables** and copy `DATABASE_URL`.

### Step 4 - Set environment variables (API service)

Add at least:

```
ConnectionStrings__DefaultConnection = <Npgsql format connection string>
OpenAI__ApiKey                       = sk-...
Telegram__Enabled                    = true
Telegram__BotToken                   = <token from BotFather>
Telegram__WebhookSecret              = <your secret>
```

Optional Graph/OneDrive mode:

```
Graph__TenantId                      = consumers
Graph__ClientId                      = <entra app client id>
Graph__RootPath                      = /Apps/Flashcards Deluxe
VocabularyStorage__DefaultMode       = graph
```

Optional Notion vocabulary export worker:

```
Notion__Enabled                      = true
Notion__ApiKey                       = <notion integration token>
Notion__DatabaseId                   = <notion vocabulary db id>
NotionSyncWorker__Enabled            = true
```

Optional Notion food sync (3 databases):

```
NotionFood__Enabled                  = true
NotionFood__ApiKey                   = <notion integration token>
NotionFood__InventoryDatabaseId      = <db id>
NotionFood__MealPlansDatabaseId      = <db id>
NotionFood__GroceryListDatabaseId    = <db id>
FoodSyncWorker__Enabled              = true
```

`DATABASE_URL` note:
- Railway provides URL format: `postgresql://user:pass@host:port/db`.
- App expects `ConnectionStrings__DefaultConnection` in Npgsql format:
  `Host=<host>;Port=<port>;Database=<db>;Username=<user>;Password=<pass>`.

### Step 5 - Deploy

Push to your deployment branch. Railway redeploys automatically.

Important:
- Do **not** run EF CLI manually on Railway (`dotnet ef migrations add` / `dotnet ef database update`).
- Migrations are already in repo and are applied automatically on app startup (`Database.Migrate()`).

### Step 6 - Register Telegram webhook

After deploy, get your Railway URL (for example `https://your-app.up.railway.app`) and run:

```
https://api.telegram.org/bot<TOKEN>/setWebhook?url=https://your-app.up.railway.app/api/telegram/webhook&secret_token=<WEBHOOK_SECRET>
```

Check webhook status:

```
https://api.telegram.org/bot<TOKEN>/getWebhookInfo
```

Or use helper script:

```powershell
./scripts/telegram-webhook.ps1 `
  -BotToken "<token>" `
  -PublicBaseUrl "https://your-app.up.railway.app" `
  -WebhookSecret "<secret>"
```

### Step 7 - Verify

```
https://your-app.up.railway.app/health
```

Send a message to your Telegram bot and verify logs in Railway.

---

## API natural intents

For `POST /api/conversation/messages`, you can send natural language command-like requests (no slash required), for example:

- Optional request fields: `channel`, `userId`, `conversationId`, `storageMode` (`local|graph`).
- Defaults when omitted: `channel=api`, `userId=anonymous`, `conversationId=default`.
- Example request body: `{"input":"void","channel":"telegram","userId":"mike","conversationId":"chat-42","storageMode":"graph"}`
- Response item fields now include `readyToAppend`, `suggestedPartOfSpeech`, and `duplicateMatches` to build client-side save confirmation flows.

- `show conversation history`
- `show active memory`
- `show system prompt`
- `reset prompt to default`
- `sync status`
- `show failed sync jobs`
- `run sync 25`
- `retry failed sync 10`
- `reset conversation`

Slash command forms are also supported through the same command-agent path, including:

- `/prompt history`
- `/prompt proposals`
- `/prompt set <text>`
- `/prompt propose <reason> || <text>`
- `/prompt improve <goal>`
- `/prompt apply <id>`
- `/prompt reject <id>`

Command catalog endpoints (for external clients):
- `GET /api/conversation/commands` (flat list: `category`, `command`, `description`)
- `GET /api/conversation/commands/grouped` (grouped by category)
- `GET /api/conversation/history?take=20&channel=api&userId=anonymous&conversationId=default` (recent history for exact scope)
- `GET /api/conversation/memory?take=20&channel=api&userId=anonymous&conversationId=default` (active memory for exact scope)
- `GET /api/conversation/prompt` (active system prompt)
- `GET /api/conversation/prompt/history?take=20` (system prompt versions)
- `GET /api/conversation/prompt/proposals?take=20` (pending/reviewed prompt proposals)
- `PUT /api/conversation/prompt` (set active system prompt)
- `POST /api/conversation/prompt/default` (reset system prompt to default)
- `POST /api/conversation/prompt/proposals` (create manual prompt proposal)
- `POST /api/conversation/prompt/proposals/improve` (generate AI prompt proposal)
- `POST /api/conversation/prompt/proposals/{id}/apply` (apply proposal)
- `POST /api/conversation/prompt/proposals/{id}/reject` (reject proposal)
- `POST /api/conversation/reset?channel=api&userId=anonymous&conversationId=default` (reset conversation for exact scope)
- `GET /api/session/bootstrap?channel=api&userId=anonymous&conversationId=default` (single payload for scope, preferences, Graph status, grouped commands, and POS marker options; optional flags: `includeDecks=true`, `includeCommands=false`, `includePartOfSpeechOptions=false`)
- `GET /api/graph/status` (get Graph authentication status)
- `POST /api/graph/login` (start Graph device-code login and return fresh auth status)
- `POST /api/graph/login/start` (start two-phase Graph device-code flow and return device challenge payload)
- `POST /api/graph/login/complete` (complete two-phase device-code login using returned challenge payload)
- `POST /api/graph/logout` (clear Graph token cache and return fresh auth status)
- `POST /api/telegram/webhook` (Telegram webhook adapter; maps Telegram chat/user/thread to conversation scope and sends reply via Bot API)
- `GET /api/notion-sync/status` (Notion export status + pending/failed SQL card counts)
- `POST /api/notion-sync/run?take=25` (run Notion export for pending SQL cards)
- `GET /api/notion-sync/failed?take=20` (list failed Notion export cards)
- `POST /api/notion-sync/retry-failed?take=25` (move failed Notion export cards back to pending)
- `GET /api/vocabulary-sync/failed?take=20` (list recent failed sync jobs)
- `POST /api/vocabulary-sync/retry-failed?take=25` (move failed jobs back to pending with reset attempts)
- `GET /api/preferences/save-mode` (get scoped save mode preference and available values)
- `PUT /api/preferences/save-mode` (set scoped save mode preference to one of available values)
- `GET /api/preferences/session` (get combined scoped preferences: save mode + storage mode)
- `PUT /api/preferences/session` (set one or both scoped preferences: `saveMode`, `storageMode`)
- `POST /api/vocabulary/analyze` (process one vocabulary item using scoped conversation context)
- `POST /api/vocabulary/analyze-batch` (process multiple items sequentially in one scope)
- `GET /api/vocabulary/storage-mode` (get active vocabulary storage mode and supported values)
- `PUT /api/vocabulary/storage-mode` (switch vocabulary storage mode to one of available values)
- `GET /api/vocabulary/decks` (list writable decks for current storage mode with suggested POS marker)
- `GET /api/vocabulary/markers` (list supported POS markers for custom save flows)
- `POST /api/vocabulary/parse-batch` (parse raw batch text and return split hints for clients)
- `POST /api/vocabulary/save` (append parsed assistant reply to selected deck)
- `POST /api/vocabulary/save-batch` (append multiple assistant replies in one request with per-item result)

Vocabulary scope and mode notes:
- `channel`, `userId`, `conversationId` select per-user storage preferences for vocabulary endpoints.
- `storageMode` can be sent as request override (`analyze`/`analyze-batch` body, `decks`/`save`/`save-batch` query) without changing stored preference.

Single-word inputs are still treated as vocabulary requests to avoid accidental command routing.

## Telemetry API

Use telemetry endpoint to inspect how requests are routed by channel/agent/intent:

- `GET /api/telemetry/intents?days=7&top=20&channel=api`

Query parameters:

- `days` (optional, default `7`, range `1..90`)
- `top` (optional, default `20`, range `1..200`)
- `channel` (optional, case-insensitive; examples: `api`, `ui`)

## Commands

Use `/help` to see full command reference in the console.

- `/help`
- `/batch`
- `/history`
- `/memory`
- `/save`
- `/save mode <mode>`
- `/storage`
- `/storage mode <mode>`
- `/graph status`
- `/graph login`
- `/graph logout`
- `/sync`
- `/sync status`
- `/sync failed`
- `/sync run`
- `/sync run <n>`
- `/sync retry failed`
- `/sync retry failed <n>`
- `/prompt`
- `/prompt default`
- `/prompt history`
- `/prompt set`
- `/prompt set <text>`
- `/prompt proposals`
- `/prompt propose <reason> || <text>`
- `/prompt improve <goal>`
- `/prompt apply <id>`
- `/prompt reject <id>`
- `/reset`
- `/exit`

Mode hints:
- Save modes are exposed by `GET /api/preferences/save-mode` and currently include `ask`, `auto`, `off`.
- Storage modes are exposed by `GET /api/vocabulary/storage-mode` and currently include `local`, `graph`.

## Prompt behavior notes

- For a new database, the default system prompt from `AssistantDefaults.SystemPrompt` is seeded as version 1.
- For an existing database with an active prompt, the active prompt remains unchanged until you update it.

## Verify

```powershell
dotnet build LagerthaAssistant.sln
dotnet test LagerthaAssistant.sln -v minimal
```

