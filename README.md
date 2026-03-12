# LagerthaAssistant

Console AI assistant prototype built with Clean Architecture and SQL Server persistence.

## Projects

- `src/LagerthaAssistant.Domain` - domain rules, entities, shared abstractions.
- `src/LagerthaAssistant.Application` - use-case services, interfaces, prompt/memory/vocabulary parsing logic.
- `src/LagerthaAssistant.Infrastructure` - EF Core, repositories, migrations, OpenAI HTTP client, local Excel + OneDrive Graph deck integration.
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
- In `ask` mode app prompts once at the end to save all, review targets, or skip.

Irregular verbs are supported via a dedicated deck (for example `beat - beat - beaten`). Duplicate lookup matches by any form (base/past/participle).

Excel columns used:

- `A` - meanings (`(n) ...`, `(v) ...`) with line breaks.
- `B` - English word.
- `H` - example sentences with line breaks.

Read-only (composite) decks are configured via `VocabularyDecks.ReadOnlyFileNames` and are never written.

SQL index + sync queue:

- App stores indexed vocabulary records in SQL Server (`VocabularyCards`, `VocabularyCardTokens`) for fast duplicate lookup before Excel scan.
- Excel/OneDrive remains the final export source for your mobile app workflow.
- If write to Excel fails due lock/conflict, app stores a pending sync job in SQL (`VocabularySyncJobs`) so data is not lost and can be retried by worker flow.

## Configuration

Set OpenAI API key (required):

```powershell
$env:OPENAI_API_KEY = "your_api_key"
```

Connection string and deck settings are configured in:

- `src/LagerthaAssistant.UI/appsettings.json`
- `src/LagerthaAssistant.Api/appsettings.json`

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

## Run

Console UI:

```powershell
dotnet run --project src/LagerthaAssistant.UI/LagerthaAssistant.UI.csproj
```

API service (Phase A foundation for multi-channel clients and agents):

```powershell
dotnet run --project src/LagerthaAssistant.Api/LagerthaAssistant.Api.csproj
```

Quick checks:

```powershell
curl http://localhost:5000/health
curl -X POST http://localhost:5000/api/conversation/messages -H "Content-Type: application/json" -d "{\"input\":\"void\"}"
curl http://localhost:5000/api/vocabulary-sync/status
curl -X POST "http://localhost:5000/api/vocabulary-sync/run?take=25"
curl "http://localhost:5000/api/telemetry/intents?days=7&top=20&channel=api"
curl http://localhost:5000/api/conversation/commands
```

On startup both UI and API apply EF migrations automatically.

## API natural intents

For `POST /api/conversation/messages`, you can send natural language command-like requests (no slash required), for example:

- Optional request field `channel` can be used for multi-channel clients (`api` by default).
- Example request body: `{"input":"void","channel":"telegram"}`

- `show conversation history`
- `show active memory`
- `show system prompt`
- `reset prompt to default`
- `sync status`
- `run sync 25`
- `reset conversation`

Slash command forms are also supported through the same command-agent path, including:

- `/prompt history`
- `/prompt proposals`
- `/prompt set <text>`
- `/prompt propose <reason> || <text>`
- `/prompt improve <goal>`
- `/prompt apply <id>`
- `/prompt reject <id>`

Command catalog endpoint (for external clients):
- `GET /api/conversation/commands`

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
- `/save mode ask|auto|off`
- `/storage`
- `/storage mode local|graph`
- `/graph status`
- `/graph login`
- `/graph logout`
- `/sync`
- `/sync status`
- `/sync run`
- `/sync run <n>`
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

## Prompt behavior notes

- For a new database, the default system prompt from `AssistantDefaults.SystemPrompt` is seeded as version 1.
- For an existing database with an active prompt, the active prompt remains unchanged until you update it.

## Verify

```powershell
dotnet build LagerthaAssistant.slnx
dotnet test LagerthaAssistant.slnx -v minimal
```
