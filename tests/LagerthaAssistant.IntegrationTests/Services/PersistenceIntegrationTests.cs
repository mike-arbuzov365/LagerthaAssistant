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
    private static string CreateConnectionString()
    {
        return $"Server=(localdb)\\MSSQLLocalDB;Database=LagerthaAssistant_Integration_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True;";
    }

    private static AppDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
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









