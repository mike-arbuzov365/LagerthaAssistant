# 06 — Testing

> Fast feedback loop: unit tests run in milliseconds, integration tests need Docker.

---

## Current Test Count (updated: 2026-03-29)

| Project | Count | Type | Status |
|---|---|---|---|
| `LagerthaAssistant.Domain.Tests` | 5 | Unit | Green |
| `LagerthaAssistant.Application.Tests` | 491 | Unit | Green |
| `LagerthaAssistant.IntegrationTests` | 330 | Integration (Testcontainers) | Green |
| `BaguetteDesign.Tests` | 12 | Unit | Green |
| **Total** | **838** | | **All green** |

---

## Test Pyramid

```
         E2E (manual only)
        ─────────────────
       Integration (~330)
      ───────────────────
     Unit (~508)
    ─────────────────────
```

- **Unit** — business logic only; no DB, no HTTP, no Telegram
- **Integration** — real PostgreSQL via Testcontainers; tests EF migrations and queries
- **E2E** — manual testing in Telegram (screenshot/video before merge)

---

## What We Test

**Unit tests (fast, no infrastructure):**
- Domain logic: `RoleRouter`, `BriefValidator`, conversation rules
- Application handlers: `StartCommandHandler`, `QuestionHandler`
- Using fakes (hand-written stubs), not Moq

**Integration tests (require Docker):**
- EF migrations applied correctly to a fresh database
- Repository queries return correct data
- `AppDbContext` and `BaguetteDbContext` model consistency

**What we do NOT test:**
- EF Core internals (it's already tested by Microsoft)
- DI registration / Program.cs wiring
- External APIs (Notion, Google) — mock at the boundary
- `appsettings.json` structure

---

## Fake Pattern (no Moq)

All fakes are hand-written inner classes in the test file. This keeps tests readable and avoids Moq magic.

```csharp
// BaguetteDesign.Tests/QuestionHandlerTests.cs
private sealed class FakeAiChatClient : IAiChatClient
{
    private readonly string _reply;
    public List<ConversationMessage> ReceivedMessages { get; } = [];

    public FakeAiChatClient(string reply) => _reply = reply;

    public Task<AssistantCompletionResult> CompleteAsync(
        IReadOnlyCollection<ConversationMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ReceivedMessages.AddRange(messages);
        return Task.FromResult(new AssistantCompletionResult(_reply, "fake-model", null));
    }
}
```

```csharp
// BaguetteDesign.Tests/StartCommandHandlerTests.cs
private sealed class FakeTelegramSender : ITelegramBotSender
{
    public List<(long ChatId, string Text)> SentMessages { get; } = [];

    public Task<TelegramSendResult> SendTextAsync(
        long chatId, string text,
        TelegramSendOptions? options = null,
        int? messageThreadId = null,
        CancellationToken cancellationToken = default)
    {
        SentMessages.Add((chatId, text));
        return Task.FromResult(new TelegramSendResult(true));
    }
    // ...
}
```

---

## Integration Tests Setup

Uses `Testcontainers` — spins up a real PostgreSQL container per test class.

```csharp
// LagerthaAssistant.IntegrationTests (pattern)
public class SomeRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder().Build();

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        // apply EF migrations against real DB
    }

    public Task DisposeAsync() => _db.DisposeAsync().AsTask();
}
```

Docker must be running locally for integration tests to pass.

---

## Commands

```bash
# Full suite (requires Docker for integration tests)
dotnet test BotPlatform.sln

# Only BaguetteDesign unit tests
dotnet test tests/BaguetteDesign.Tests

# Only Lagertha unit tests (fast, no Docker)
dotnet test tests/LagerthaAssistant.Application.Tests
dotnet test tests/LagerthaAssistant.Domain.Tests

# Lagertha integration tests (requires Docker)
dotnet test tests/LagerthaAssistant.IntegrationTests

# With output (see test names)
dotnet test BotPlatform.sln --logger "console;verbosity=normal"
```

---

## Rules

1. Every new handler gets at least 2 unit tests: happy path + edge case
2. Fakes live in the test file as private inner classes
3. Integration tests use a real DB — never mock EF DbContext
4. No test should depend on another test's side effects (no shared state)
5. A PR with failing tests is not merged, period
