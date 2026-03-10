# LagerthaAssistant

Console AI assistant prototype built with Clean Architecture and SQL Server persistence.

## Projects

- `src/LagerthaAssistant.Domain` - domain rules, entities, shared abstractions.
- `src/LagerthaAssistant.Application` - use-case services, interfaces, prompt/memory/vocabulary parsing logic.
- `src/LagerthaAssistant.Infrastructure` - EF Core, repositories, migrations, OpenAI HTTP client, Excel deck integration.
- `src/LagerthaAssistant.UI` - interactive console app and command routing.
- `tests/LagerthaAssistant.*` - domain, application, and integration tests.

## Core capabilities

- Persistent conversation history (`ConversationSessions`, `ConversationHistoryEntries`).
- Persistent user memory (`UserMemoryEntries`) injected into next requests.
- Persistent versioned system prompts (`SystemPromptEntries`).
- Prompt proposal workflow (`SystemPromptProposals`) with apply/reject flow.
- Vocabulary workflow with Excel (`.xlsx`) decks in OneDrive folder.

## Vocabulary workflow

When you type a word or phrase (non-command input):

1. Assistant checks duplicates only in writable decks.
2. If found, it prints saved data from Excel and skips AI call.
3. If not found, it calls AI and parses the response.
4. Before write, app decides by save mode: `ask` (confirm), `auto` (write immediately), `off` (skip writing).

Irregular verbs are supported via a dedicated deck (for example `beat - beat - beaten`). Duplicate lookup matches by any form (base/past/participle).

Excel columns used:

- `A` - meanings (`(n) ...`, `(v) ...`) with line breaks.
- `B` - English word.
- `H` - example sentences with line breaks.

Read-only (composite) decks are configured via `VocabularyDecks.ReadOnlyFileNames` and are never written.

## Configuration

Set OpenAI API key (required):

```powershell
$env:OPENAI_API_KEY = "your_api_key"
```

Connection string and vocabulary deck settings are configured in:

- `src/LagerthaAssistant.UI/appsettings.json`

Example vocabulary section:

```json
"VocabularyDecks": {
  "FolderPath": "%OneDrive%\\Apps\\Flashcards Deluxe",
  "FilePattern": "wm-*.xlsx",
  "IrregularVerbDeckFileName": "wm-irregular-verbs-ua-en.xlsx",
  "ReadOnlyFileNames": [
    "wm-vocabulary-all-ru-en.xlsx",
    "wm-training-all-ru-en.xlsx",
    "wm-grammar-all-ru-en.xlsx"
  ]
}
```

## Run

```powershell
dotnet run --project src/LagerthaAssistant.UI/LagerthaAssistant.UI.csproj
```

On startup the app applies EF migrations automatically.

## Commands

Use `/help` to see full command reference in the console.

- `/help`
- `/history`
- `/memory`
- `/save`
- `/save mode ask|auto|off`
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





