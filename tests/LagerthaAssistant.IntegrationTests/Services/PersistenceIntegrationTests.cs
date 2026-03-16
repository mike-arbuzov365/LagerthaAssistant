namespace LagerthaAssistant.IntegrationTests.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Services;
using LagerthaAssistant.Application.Services.Memory;
using LagerthaAssistant.Domain.AI;
using LagerthaAssistant.Domain.Abstractions;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Domain.Enums;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.Infrastructure.Repositories;
using Xunit;

public sealed class PersistenceIntegrationTests
{
    [Fact]
    public async Task ConversationHistoryRepository_ShouldReturnRecentHistoryInChronologicalOrder()
    {
        var connectionString = CreateConnectionString();
        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();

        try
        {
            var session = ConversationSession.Create(Guid.NewGuid(), "session");
            context.ConversationSessions.Add(session);
            await context.SaveChangesAsync();

            context.ConversationHistoryEntries.AddRange(
                ConversationHistoryEntry.Create(session, MessageRole.User, "u1", new DateTimeOffset(2026, 3, 8, 10, 0, 0, TimeSpan.Zero)),
                ConversationHistoryEntry.Create(session, MessageRole.Assistant, "a1", new DateTimeOffset(2026, 3, 8, 10, 1, 0, TimeSpan.Zero)),
                ConversationHistoryEntry.Create(session, MessageRole.User, "u2", new DateTimeOffset(2026, 3, 8, 10, 2, 0, TimeSpan.Zero)));
            await context.SaveChangesAsync();

            await using var actContext = CreateContext(connectionString);
            var sut = new ConversationHistoryRepository(actContext, NullLogger<ConversationHistoryRepository>.Instance);

            var history = await sut.GetRecentBySessionIdAsync(session.Id, 2);

            Assert.Equal(2, history.Count);
            Assert.Equal("a1", history[0].Content);
            Assert.Equal("u2", history[1].Content);
        }
        finally
        {
            await CleanupDatabaseAsync(connectionString);
        }
    }

    [Fact]
    public async Task UnitOfWork_Rollback_ShouldCancelPersistedChangesInsideTransaction()
    {
        var connectionString = CreateConnectionString();
        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();

        try
        {
            var unitOfWork = new UnitOfWork(context, NullLogger<UnitOfWork>.Instance);
            var sessions = new ConversationSessionRepository(context, NullLogger<ConversationSessionRepository>.Instance);

            await unitOfWork.BeginTransactionAsync();
            await sessions.AddAsync(ConversationSession.Create(Guid.NewGuid(), "to-rollback"));
            await unitOfWork.SaveChangesAsync();
            await unitOfWork.RollbackTransactionAsync();

            await using var verifyContext = CreateContext(connectionString);
            var allSessions = await verifyContext.ConversationSessions.ToListAsync();
            Assert.Empty(allSessions);
        }
        finally
        {
            await CleanupDatabaseAsync(connectionString);
        }
    }

    [Fact]
    public async Task AssistantSessionService_ShouldPersistAndReuseMemoryAcrossCalls()
    {
        var connectionString = CreateConnectionString();
        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();

        try
        {
            var ai = new FakeAiChatClient();
            var sut = new AssistantSessionService(
                ai,
                new ConversationSessionRepository(context, NullLogger<ConversationSessionRepository>.Instance),
                new ConversationHistoryRepository(context, NullLogger<ConversationHistoryRepository>.Instance),
                new UserMemoryRepository(context, NullLogger<UserMemoryRepository>.Instance),
                new SystemPromptRepository(context, NullLogger<SystemPromptRepository>.Instance),
                new SystemPromptProposalRepository(context, NullLogger<SystemPromptProposalRepository>.Instance),
                new ConversationMemoryExtractor(),
                new UnitOfWork(context, NullLogger<UnitOfWork>.Instance),
                new AssistantSessionOptions { SystemPrompt = "system prompt", MaxHistoryMessages = 20 },
                new FakeClock(),
                new FakeConversationScopeAccessor(),
                NullLogger<AssistantSessionService>.Instance);

            await sut.AskAsync("my name is Lagertha and i prefer english");
            await sut.AskAsync("hello again");

            await using var verifyContext = CreateContext(connectionString);
            var history = await verifyContext.ConversationHistoryEntries.ToListAsync();
            var memory = await verifyContext.UserMemoryEntries.ToListAsync();

            Assert.Equal(4, history.Count);
            Assert.Contains(memory, x => x.Key == MemoryKeys.UserName && x.Value == "Lagertha");
            Assert.Contains(memory, x => x.Key == MemoryKeys.PreferredLanguage && x.Value == "en");

            Assert.NotNull(ai.LastMessages);
            Assert.Contains(
                ai.LastMessages!,
                x => x.Role == MessageRole.System && x.Content.Contains(MemoryKeys.UserName, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await CleanupDatabaseAsync(connectionString);
        }
    }

    [Fact]
    public async Task ConversationHistoryRepository_ShouldKeepStableOrderWhenTimestampsAreEqual()
    {
        var connectionString = CreateConnectionString();
        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();

        try
        {
            var session = ConversationSession.Create(Guid.NewGuid(), "session");
            context.ConversationSessions.Add(session);
            await context.SaveChangesAsync();

            var sentAt = new DateTimeOffset(2026, 3, 8, 10, 0, 0, TimeSpan.Zero);
            context.ConversationHistoryEntries.AddRange(
                ConversationHistoryEntry.Create(session, MessageRole.User, "u1", sentAt),
                ConversationHistoryEntry.Create(session, MessageRole.Assistant, "a1", sentAt));
            await context.SaveChangesAsync();

            await using var actContext = CreateContext(connectionString);
            var sut = new ConversationHistoryRepository(actContext, NullLogger<ConversationHistoryRepository>.Instance);

            var history = await sut.GetRecentBySessionIdAsync(session.Id, 2);

            Assert.Equal(2, history.Count);
            Assert.Equal(MessageRole.User, history[0].Role);
            Assert.Equal("u1", history[0].Content);
            Assert.Equal(MessageRole.Assistant, history[1].Role);
            Assert.Equal("a1", history[1].Content);
        }
        finally
        {
            await CleanupDatabaseAsync(connectionString);
        }
    }

    [Fact]
    public async Task ConversationSessionRepository_GetLatestAsync_ShouldFilterByScope()
    {
        var connectionString = CreateConnectionString();
        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();

        try
        {
            context.ConversationSessions.Add(ConversationSession.Create(Guid.NewGuid(), "other-scope", "api", "user-b", "chat-1"));
            await context.SaveChangesAsync();

            var first = ConversationSession.Create(Guid.NewGuid(), "scope-a-first", "api", "user-a", "chat-1");
            context.ConversationSessions.Add(first);
            await context.SaveChangesAsync();

            await Task.Delay(20);

            var second = ConversationSession.Create(Guid.NewGuid(), "scope-a-second", "api", "user-a", "chat-1");
            context.ConversationSessions.Add(second);
            await context.SaveChangesAsync();

            await using var actContext = CreateContext(connectionString);
            var repository = new ConversationSessionRepository(actContext, NullLogger<ConversationSessionRepository>.Instance);

            var latest = await repository.GetLatestAsync("api", "user-a", "chat-1");

            Assert.NotNull(latest);
            Assert.Equal(second.SessionKey, latest!.SessionKey);
            Assert.Equal("api", latest.Channel);
            Assert.Equal("user-a", latest.UserId);
            Assert.Equal("chat-1", latest.ConversationId);
        }
        finally
        {
            await CleanupDatabaseAsync(connectionString);
        }
    }

    [Fact]
    public async Task UserMemoryRepository_ShouldFilterByScope()
    {
        var connectionString = CreateConnectionString();
        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();

        try
        {
            context.UserMemoryEntries.AddRange(
                new UserMemoryEntry
                {
                    Channel = "api",
                    UserId = "user-a",
                    Key = "ui.save.mode",
                    Value = "ask",
                    Confidence = 1.0,
                    IsActive = false,
                    LastSeenAtUtc = DateTimeOffset.UtcNow
                },
                new UserMemoryEntry
                {
                    Channel = "api",
                    UserId = "user-b",
                    Key = "ui.save.mode",
                    Value = "auto",
                    Confidence = 1.0,
                    IsActive = false,
                    LastSeenAtUtc = DateTimeOffset.UtcNow
                },
                new UserMemoryEntry
                {
                    Channel = "api",
                    UserId = "user-a",
                    Key = MemoryKeys.UserName,
                    Value = "Alice",
                    Confidence = 1.0,
                    IsActive = true,
                    LastSeenAtUtc = DateTimeOffset.UtcNow
                },
                new UserMemoryEntry
                {
                    Channel = "api",
                    UserId = "user-b",
                    Key = MemoryKeys.UserName,
                    Value = "Bob",
                    Confidence = 1.0,
                    IsActive = true,
                    LastSeenAtUtc = DateTimeOffset.UtcNow
                });

            await context.SaveChangesAsync();

            await using var actContext = CreateContext(connectionString);
            var repository = new UserMemoryRepository(actContext, NullLogger<UserMemoryRepository>.Instance);

            var saveMode = await repository.GetByKeyAsync("ui.save.mode", "api", "user-a");
            var active = await repository.GetActiveAsync(10, "api", "user-a");

            Assert.NotNull(saveMode);
            Assert.Equal("ask", saveMode!.Value);

            var userName = Assert.Single(active, x => x.Key == MemoryKeys.UserName);
            Assert.Equal("Alice", userName.Value);
            Assert.DoesNotContain(active, x => x.UserId == "user-b");
        }
        finally
        {
            await CleanupDatabaseAsync(connectionString);
        }
    }

    [Fact]
    public async Task AssistantSessionService_ShouldIsolateSessionsAndMemoryByScopeInDatabase()
    {
        var connectionString = CreateConnectionString();
        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();

        try
        {
            var ai = new FakeAiChatClient();
            var scopeAccessor = new FakeConversationScopeAccessor();
            var sut = new AssistantSessionService(
                ai,
                new ConversationSessionRepository(context, NullLogger<ConversationSessionRepository>.Instance),
                new ConversationHistoryRepository(context, NullLogger<ConversationHistoryRepository>.Instance),
                new UserMemoryRepository(context, NullLogger<UserMemoryRepository>.Instance),
                new SystemPromptRepository(context, NullLogger<SystemPromptRepository>.Instance),
                new SystemPromptProposalRepository(context, NullLogger<SystemPromptProposalRepository>.Instance),
                new ConversationMemoryExtractor(),
                new UnitOfWork(context, NullLogger<UnitOfWork>.Instance),
                new AssistantSessionOptions { SystemPrompt = "system prompt", MaxHistoryMessages = 20 },
                new FakeClock(),
                scopeAccessor,
                NullLogger<AssistantSessionService>.Instance);

            scopeAccessor.Set(ConversationScope.Create("api", "user-a", "chat-a"));
            await sut.AskAsync("my name is Lagertha");

            scopeAccessor.Set(ConversationScope.Create("api", "user-b", "chat-b"));
            await sut.AskAsync("my name is Mike");

            await using var verifyContext = CreateContext(connectionString);
            var sessions = await verifyContext.ConversationSessions.AsNoTracking().ToListAsync();
            var memories = await verifyContext.UserMemoryEntries
                .AsNoTracking()
                .Where(x => x.Key == MemoryKeys.UserName)
                .ToListAsync();

            Assert.Contains(sessions, x => x.Channel == "api" && x.UserId == "user-a" && x.ConversationId == "chat-a");
            Assert.Contains(sessions, x => x.Channel == "api" && x.UserId == "user-b" && x.ConversationId == "chat-b");

            Assert.Contains(memories, x => x.Channel == "api" && x.UserId == "user-a" && x.Value == "Lagertha");
            Assert.Contains(memories, x => x.Channel == "api" && x.UserId == "user-b" && x.Value == "Mike");
        }
        finally
        {
            await CleanupDatabaseAsync(connectionString);
        }
    }

    [Fact]
    public async Task VocabularySyncJobRepository_ClaimPendingAsync_ShouldAvoidDoubleClaimAcrossConcurrentRuns()
    {
        var connectionString = CreateConnectionString();
        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();

        try
        {
            context.VocabularySyncJobs.AddRange(
                new VocabularySyncJob
                {
                    RequestedWord = "void",
                    AssistantReply = "void",
                    TargetDeckFileName = "wm-nouns-ua-en.xlsx",
                    StorageMode = "local",
                    Status = VocabularySyncJobStatus.Pending,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-3)
                },
                new VocabularySyncJob
                {
                    RequestedWord = "prepare",
                    AssistantReply = "prepare",
                    TargetDeckFileName = "wm-verbs-us-en.xlsx",
                    StorageMode = "local",
                    Status = VocabularySyncJobStatus.Pending,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2)
                },
                new VocabularySyncJob
                {
                    RequestedWord = "call back",
                    AssistantReply = "call back",
                    TargetDeckFileName = "wm-phrasal-verbs-ua-en.xlsx",
                    StorageMode = "local",
                    Status = VocabularySyncJobStatus.Pending,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
                });
            await context.SaveChangesAsync();

            await using var actContextA = CreateContext(connectionString);
            await using var actContextB = CreateContext(connectionString);
            var repoA = new VocabularySyncJobRepository(actContextA, NullLogger<VocabularySyncJobRepository>.Instance);
            var repoB = new VocabularySyncJobRepository(actContextB, NullLogger<VocabularySyncJobRepository>.Instance);

            var claimedA = await repoA.ClaimPendingAsync(2, DateTimeOffset.UtcNow);
            var claimedB = await repoB.ClaimPendingAsync(2, DateTimeOffset.UtcNow);

            var claimedIds = claimedA.Select(x => x.Id)
                .Concat(claimedB.Select(x => x.Id))
                .ToList();

            Assert.Equal(claimedIds.Count, claimedIds.Distinct().Count());
            Assert.Equal(3, claimedIds.Count);

            await using var verifyContext = CreateContext(connectionString);
            var jobs = await verifyContext.VocabularySyncJobs.AsNoTracking().ToListAsync();
            Assert.All(jobs, job => Assert.Equal(VocabularySyncJobStatus.Processing, job.Status));
        }
        finally
        {
            await CleanupDatabaseAsync(connectionString);
        }
    }

    [Fact]
    public async Task VocabularySyncJobRepository_FindActiveDuplicateAsync_ShouldFindPendingAndProcessingJobs()
    {
        var connectionString = CreateConnectionString();
        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();

        try
        {
            context.VocabularySyncJobs.AddRange(
                new VocabularySyncJob
                {
                    RequestedWord = "void",
                    AssistantReply = "void\n\n(n) emptiness",
                    TargetDeckFileName = "wm-nouns-ua-en.xlsx",
                    StorageMode = "local",
                    Status = VocabularySyncJobStatus.Pending,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2)
                },
                new VocabularySyncJob
                {
                    RequestedWord = "prepare",
                    AssistantReply = "prepare\n\n(v) get ready",
                    TargetDeckFileName = "wm-verbs-us-en.xlsx",
                    StorageMode = "graph",
                    Status = VocabularySyncJobStatus.Processing,
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
                });
            await context.SaveChangesAsync();

            await using var actContext = CreateContext(connectionString);
            var repo = new VocabularySyncJobRepository(actContext, NullLogger<VocabularySyncJobRepository>.Instance);

            var pending = await repo.FindActiveDuplicateAsync(
                "void",
                "void\n\n(n) emptiness",
                "wm-nouns-ua-en.xlsx",
                "local",
                null);

            var processing = await repo.FindActiveDuplicateAsync(
                "prepare",
                "prepare\n\n(v) get ready",
                "wm-verbs-us-en.xlsx",
                "graph",
                null);

            Assert.NotNull(pending);
            Assert.NotNull(processing);
            Assert.Equal(VocabularySyncJobStatus.Pending, pending!.Status);
            Assert.Equal(VocabularySyncJobStatus.Processing, processing!.Status);
        }
        finally
        {
            await CleanupDatabaseAsync(connectionString);
        }
    }

    [Fact]
    public async Task VocabularySyncJobRepository_GetFailedAsync_ShouldReturnLatestFailedJobsFirst()
    {
        var connectionString = CreateConnectionString();
        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();

        try
        {
            context.VocabularySyncJobs.AddRange(
                new VocabularySyncJob
                {
                    RequestedWord = "old",
                    AssistantReply = "old",
                    TargetDeckFileName = "wm-nouns-ua-en.xlsx",
                    StorageMode = "local",
                    Status = VocabularySyncJobStatus.Failed,
                    LastError = "old error",
                    LastAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-20)
                },
                new VocabularySyncJob
                {
                    RequestedWord = "new",
                    AssistantReply = "new",
                    TargetDeckFileName = "wm-verbs-us-en.xlsx",
                    StorageMode = "local",
                    Status = VocabularySyncJobStatus.Failed,
                    LastError = "new error",
                    LastAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-3)
                });
            await context.SaveChangesAsync();

            await using var actContext = CreateContext(connectionString);
            var repo = new VocabularySyncJobRepository(actContext, NullLogger<VocabularySyncJobRepository>.Instance);

            var failed = await repo.GetFailedAsync(10);

            Assert.Equal(2, failed.Count);
            Assert.Equal("new", failed[0].RequestedWord);
            Assert.Equal("old", failed[1].RequestedWord);
        }
        finally
        {
            await CleanupDatabaseAsync(connectionString);
        }
    }

    [Fact]
    public async Task VocabularySyncJobRepository_RequeueFailedAsync_ShouldMoveFailedJobsBackToPending()
    {
        var connectionString = CreateConnectionString();
        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();

        try
        {
            context.VocabularySyncJobs.Add(new VocabularySyncJob
            {
                RequestedWord = "void",
                AssistantReply = "void",
                TargetDeckFileName = "wm-nouns-ua-en.xlsx",
                StorageMode = "local",
                Status = VocabularySyncJobStatus.Failed,
                AttemptCount = 8,
                LastError = "Retry limit reached",
                LastAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
                CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2)
            });
            await context.SaveChangesAsync();

            await using var actContext = CreateContext(connectionString);
            var repo = new VocabularySyncJobRepository(actContext, NullLogger<VocabularySyncJobRepository>.Instance);
            var requeued = await repo.RequeueFailedAsync(10, DateTimeOffset.UtcNow);
            await actContext.SaveChangesAsync();

            Assert.Equal(1, requeued);

            await using var verifyContext = CreateContext(connectionString);
            var job = await verifyContext.VocabularySyncJobs.AsNoTracking().SingleAsync();
            Assert.Equal(VocabularySyncJobStatus.Pending, job.Status);
            Assert.Equal(0, job.AttemptCount);
            Assert.Null(job.LastError);
            Assert.Null(job.LastAttemptAtUtc);
            Assert.Null(job.CompletedAtUtc);
        }
        finally
        {
            await CleanupDatabaseAsync(connectionString);
        }
    }
    [Fact]
    public async Task TelegramProcessedUpdateRepository_IsProcessedAsync_ShouldReturnFalseForUnseenUpdate()
    {
        var connectionString = CreateConnectionString();
        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();

        try
        {
            var sut = new TelegramProcessedUpdateRepository(context, NullLogger<TelegramProcessedUpdateRepository>.Instance);

            var result = await sut.IsProcessedAsync(999L);

            Assert.False(result);
        }
        finally
        {
            await CleanupDatabaseAsync(connectionString);
        }
    }

    [Fact]
    public async Task TelegramProcessedUpdateRepository_MarkProcessedAsync_ShouldPersistAndBeDetectedAsProcessed()
    {
        var connectionString = CreateConnectionString();
        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();

        try
        {
            var sut = new TelegramProcessedUpdateRepository(context, NullLogger<TelegramProcessedUpdateRepository>.Instance);

            await sut.MarkProcessedAsync(42L);

            await using var verifyContext = CreateContext(connectionString);
            var verifySut = new TelegramProcessedUpdateRepository(verifyContext, NullLogger<TelegramProcessedUpdateRepository>.Instance);
            Assert.True(await verifySut.IsProcessedAsync(42L));
            Assert.False(await verifySut.IsProcessedAsync(43L));
        }
        finally
        {
            await CleanupDatabaseAsync(connectionString);
        }
    }

    [Fact]
    public async Task TelegramProcessedUpdateRepository_DeleteOlderThanAsync_ShouldRemoveOldRecordsAndKeepRecent()
    {
        var connectionString = CreateConnectionString();
        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();

        try
        {
            var cutoff = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);

            context.TelegramProcessedUpdates.AddRange(
                new TelegramProcessedUpdate { UpdateId = 1, ProcessedAtUtc = cutoff.AddDays(-2) },
                new TelegramProcessedUpdate { UpdateId = 2, ProcessedAtUtc = cutoff.AddDays(-1) },
                new TelegramProcessedUpdate { UpdateId = 3, ProcessedAtUtc = cutoff.AddHours(1) });
            await context.SaveChangesAsync();

            await using var actContext = CreateContext(connectionString);
            var sut = new TelegramProcessedUpdateRepository(actContext, NullLogger<TelegramProcessedUpdateRepository>.Instance);
            await sut.DeleteOlderThanAsync(cutoff);

            await using var verifyContext = CreateContext(connectionString);
            var remaining = await verifyContext.TelegramProcessedUpdates.AsNoTracking().ToListAsync();
            Assert.Single(remaining);
            Assert.Equal(3L, remaining[0].UpdateId);
        }
        finally
        {
            await CleanupDatabaseAsync(connectionString);
        }
    }

    private static string CreateConnectionString()
    {
        return $"Host=localhost;Database=lagertha_integration_{Guid.NewGuid():N};Username=postgres;Password=masterkey";
    }

    private static AppDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }

    private static async Task CleanupDatabaseAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        await context.Database.EnsureDeletedAsync();
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 3, 8, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeConversationScopeAccessor : IConversationScopeAccessor
    {
        public ConversationScope Current { get; private set; } = ConversationScope.Default;

        public void Set(ConversationScope scope)
        {
            Current = scope;
        }
    }

    private sealed class FakeAiChatClient : IAiChatClient
    {
        public IReadOnlyCollection<ConversationMessage>? LastMessages { get; private set; }

        public Task<AssistantCompletionResult> CompleteAsync(IReadOnlyCollection<ConversationMessage> messages, CancellationToken cancellationToken = default)
        {
            LastMessages = messages;
            return Task.FromResult(new AssistantCompletionResult("ok", "integration-test-model", null));
        }
    }
}









