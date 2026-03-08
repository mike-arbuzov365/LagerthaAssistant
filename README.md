# LagerthaAssistant.CleanConsole

Console AI assistant prototype using OpenAI API in Clean Architecture.

## Structure

- `src/LagerthaAssistant.Domain`: domain model, rules, entities
- `src/LagerthaAssistant.Application`: use cases, interfaces, `IUnitOfWork`, memory extraction
- `src/LagerthaAssistant.Infrastructure`: EF Core (SQL Server), repositories, unit of work, OpenAI client
- `src/LagerthaAssistant.UI`: console host and chat loop

## Persistence and Memory

Conversation data is stored in SQL Server tables:

- `ConversationSessions`
- `ConversationHistoryEntries`
- `UserMemoryEntries`

Behavior:

- On startup, assistant loads recent history from the latest session.
- `/history` shows only a recent slice (`HistoryPreviewTake`), not full history.
- `/memory` (or `/mem`) shows currently stored active user memory facts.
- Assistant extracts important facts from user messages (e.g., name/language) and stores them in `UserMemoryEntries`.
- Stored memory facts are injected into system context for next requests.

## Configure

Set OpenAI key:

```powershell
$env:OPENAI_API_KEY = "your_api_key"
```

Connection string is in `src/LagerthaAssistant.UI/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=LagerthaAssistantDb;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

## Run

```powershell
dotnet run --project src/LagerthaAssistant.UI/LagerthaAssistant.UI.csproj
```

## Verify

```powershell
dotnet build LagerthaAssistant.CleanConsole.slnx
dotnet test LagerthaAssistant.CleanConsole.slnx
```



