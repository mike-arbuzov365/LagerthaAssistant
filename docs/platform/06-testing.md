# 06 - Testing

> Fast feedback loop: unit tests run in milliseconds, integration tests need Docker.

> Authoring rules: [docs/shared/01-test-authoring-guide.md](../shared/01-test-authoring-guide.md)

---

## Current Test Count (updated: 2026-04-01)

| Project | Count | Type | Status |
|---|---|---|---|
| `LagerthaAssistant.Domain.Tests` | 5 | Unit | Green |
| `LagerthaAssistant.Application.Tests` | 509 | Unit | Green |
| `LagerthaAssistant.IntegrationTests` | 352 | Integration (Testcontainers) | Green |
| `SharedBotKernel.Tests` | 61 | Unit | Green |
| `BaguetteDesign.Domain.Tests` | 24 | Unit | Green |
| `BaguetteDesign.Application.Tests` | 77 | Unit | Green |
| `BaguetteDesign.IntegrationTests` | 3 | Integration (Testcontainers) | Requires Docker |
| **Total** | **1031** | | **All green (Docker required for integration projects)** |

---

## Test Pyramid

```text
         E2E (manual only)
        -------------------
      Integration (~355)
      ---------------------
     Unit (~676)
    -----------------------
```

- **Unit** - business logic only; no DB, no HTTP, no Telegram.
- **Integration** - real PostgreSQL via Testcontainers; tests EF migrations and queries.
- **E2E** - manual testing in Telegram (screenshot/video before merge).

---

## What We Test

**Unit tests (fast, no infrastructure):**
- Domain logic: `RoleRouter`, `BriefValidator`, `BriefFlowState`, `ProjectEntity`.
- Application handlers: all BaguetteDesign and LagerthaAssistant handlers.
- SharedBotKernel: `BackgroundSyncWorkerBase.CalculateDelay`, `Conversation` + `ConversationMessage` invariants, `AiSecretProtector`, `OpenAiChatClient`, `ClaudeChatClient`, `TelegramBotSender`, `TelegramMiniAppInitDataVerifier`, `KernelServiceExtensions`, `UserMemoryEntry`, `SystemPromptEntry`, `ResolvingAiChatClient` guard.
- Using fakes (hand-written stubs), not Moq.

**Integration tests (require Docker):**
- EF migrations applied correctly to a fresh database.
- Repository queries return correct data.
- `AppDbContext` and `BaguetteDbContext` model consistency.

**What we do NOT test:**
- EF Core internals (already tested by Microsoft).
- DI registration / Program.cs wiring.
- External APIs (Notion, Google) - mock at the boundary.
- `appsettings.json` structure.

---

## Fake Pattern (no Moq)

All fakes are hand-written inner classes in the test file. This keeps tests readable and avoids mock magic.

```csharp
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

---

## Integration Tests Setup

Uses `Testcontainers` - spins up a real PostgreSQL container per test class.

```csharp
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

# SharedBotKernel unit tests
dotnet test tests/SharedBotKernel.Tests

# BaguetteDesign unit tests
dotnet test tests/BaguetteDesign.Domain.Tests
dotnet test tests/BaguetteDesign.Application.Tests

# BaguetteDesign integration tests (requires Docker)
dotnet test tests/BaguetteDesign.IntegrationTests

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

1. Every new handler gets at least 2 unit tests: happy path + edge case.
2. Fakes live in the test file as private inner classes.
3. Integration tests use a real DB - never mock EF DbContext.
4. No test should depend on another test's side effects.
5. A PR with failing tests is not merged.

---

## Post-Deploy Backlog (do not forget)

- Add 1-2 targeted integration tests for `SharedBotKernel` / `KernelDbContext` on real PostgreSQL:
  - unique index behavior for `SystemPromptEntries` (`Version`, filtered unique `IsActive = TRUE`);
  - unique composite key/index behavior for user-scoped records (`UserMemoryEntries`, `UserAiCredentials`).
- Priority: medium (not a release blocker after `BotPlatform.sln` full green run).
