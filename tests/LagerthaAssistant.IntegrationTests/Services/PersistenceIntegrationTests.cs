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







