namespace LagerthaAssistant.Application.Tests.Services;

using Microsoft.Extensions.Logging.Abstractions;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Memory;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Memory;
using LagerthaAssistant.Application.Services;
using LagerthaAssistant.Domain.AI;
using LagerthaAssistant.Domain.Abstractions;
using LagerthaAssistant.Domain.Entities;
using Xunit;

public sealed class AssistantSessionServiceTests
{
    [Fact]
    public async Task AskAsync_ShouldIncludeMemoryFactsInContext()
    {
        var fx = new Fixture();
        fx.MemoryRepo.Active.Add(new UserMemoryEntry { Key = MemoryKeys.UserName, Value = "Mike", IsActive = true, LastSeenAtUtc = fx.Clock.UtcNow });

        var sut = fx.CreateSut();

        await sut.AskAsync("hello");

        Assert.NotNull(fx.AiClient.LastMessages);
        Assert.Contains(fx.AiClient.LastMessages!, x => x.Role == MessageRole.System && x.Content.Contains("user.name: Mike", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AskAsync_ShouldPersistTwoHistoryEntriesAndCommit()
    {
        var fx = new Fixture();
        var sut = fx.CreateSut();

        await sut.AskAsync("my name is Mike");

        Assert.Equal(2, fx.HistoryRepo.Added.Count);
        Assert.Equal(MessageRole.User, fx.HistoryRepo.Added[0].Role);
        Assert.Equal(MessageRole.Assistant, fx.HistoryRepo.Added[1].Role);

        Assert.Equal(1, fx.Uow.BeginCount);
        Assert.Equal(1, fx.Uow.SaveCount);
        Assert.Equal(1, fx.Uow.CommitCount);
        Assert.Equal(0, fx.Uow.RollbackCount);
    }

    [Fact]
    public async Task AskAsync_ShouldRollback_WhenPersistenceFails()
    {
        var fx = new Fixture();
        fx.HistoryRepo.ThrowOnAdd = true;
        var sut = fx.CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.AskAsync("hello"));

        Assert.Equal(1, fx.Uow.BeginCount);
        Assert.Equal(1, fx.Uow.RollbackCount);
        Assert.Equal(0, fx.Uow.CommitCount);
    }

    [Fact]
    public async Task AskAsync_ShouldUpsertMemoryFacts()
    {
        var fx = new Fixture();
        fx.MemoryExtractor.Facts = [new MemoryFactCandidate(MemoryKeys.UserName, "Mike", 0.95)];
        var sut = fx.CreateSut();

        await sut.AskAsync("my name is Mike");

        var stored = await fx.MemoryRepo.GetByKeyAsync(MemoryKeys.UserName);
        Assert.NotNull(stored);
        Assert.Equal("Mike", stored!.Value);

        fx.MemoryExtractor.Facts = [new MemoryFactCandidate(MemoryKeys.UserName, "Michael", 0.99)];
        await sut.AskAsync("actually i am Michael");

        stored = await fx.MemoryRepo.GetByKeyAsync(MemoryKeys.UserName);
        Assert.Equal("Michael", stored!.Value);
        Assert.Equal(0.99, stored.Confidence, 3);
    }

    [Fact]
    public async Task GetRecentHistoryAsync_ShouldLoadFromLatestSession()
    {
        var fx = new Fixture();
        fx.SessionRepo.Latest = ConversationSession.Create(Guid.NewGuid(), "latest");
        fx.SessionRepo.Latest.Id = 10;
        fx.HistoryRepo.Seeded[10] =
        [
            ConversationHistoryEntry.Create(fx.SessionRepo.Latest, MessageRole.User, "u1", fx.Clock.UtcNow.AddMinutes(-2)),
            ConversationHistoryEntry.Create(fx.SessionRepo.Latest, MessageRole.Assistant, "a1", fx.Clock.UtcNow.AddMinutes(-1))
        ];

        var sut = fx.CreateSut();

        var history = await sut.GetRecentHistoryAsync(10);

        Assert.Equal(2, history.Count);
        Assert.Equal("u1", history.First().Content);
        Assert.Equal("a1", history.Last().Content);
    }

    [Fact]
    public async Task GetActiveMemoryAsync_ShouldReturnActiveMemoryFacts()
    {
        var fx = new Fixture();
        fx.MemoryRepo.Active.Add(new UserMemoryEntry
        {
            Key = MemoryKeys.UserName,
            Value = "Mike",
            Confidence = 0.95,
            IsActive = true,
            LastSeenAtUtc = fx.Clock.UtcNow
        });
        fx.MemoryRepo.Active.Add(new UserMemoryEntry
        {
            Key = "legacy.key",
            Value = "legacy",
            Confidence = 0.10,
            IsActive = false,
            LastSeenAtUtc = fx.Clock.UtcNow
        });

        var sut = fx.CreateSut();

        var memory = await sut.GetActiveMemoryAsync(10);

        var fact = Assert.Single(memory);
        Assert.Equal(MemoryKeys.UserName, fact.Key);
        Assert.Equal("Mike", fact.Value);
    }

    private sealed class Fixture
    {
        public FakeAiChatClient AiClient { get; } = new();
        public FakeConversationSessionRepository SessionRepo { get; } = new();
        public FakeConversationHistoryRepository HistoryRepo { get; } = new();
        public FakeUserMemoryRepository MemoryRepo { get; } = new();
        public FakeConversationMemoryExtractor MemoryExtractor { get; } = new();
        public FakeUnitOfWork Uow { get; } = new();
        public FakeClock Clock { get; } = new();

        public AssistantSessionService CreateSut()
        {
            return new AssistantSessionService(
                AiClient,
                SessionRepo,
                HistoryRepo,
                MemoryRepo,
                MemoryExtractor,
                Uow,
                new AssistantSessionOptions { SystemPrompt = "system prompt", MaxHistoryMessages = 20 },
                Clock,
                NullLogger<AssistantSessionService>.Instance);
        }
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 3, 6, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeAiChatClient : IAiChatClient
    {
        public IReadOnlyCollection<ConversationMessage>? LastMessages { get; private set; }

        public Task<AssistantCompletionResult> CompleteAsync(IReadOnlyCollection<ConversationMessage> messages, CancellationToken cancellationToken = default)
        {
            LastMessages = messages;
            return Task.FromResult(new AssistantCompletionResult("ok", "test-model", null));
        }
    }

    private sealed class FakeConversationSessionRepository : IConversationSessionRepository
    {
        public ConversationSession? Latest { get; set; }
        public readonly List<ConversationSession> Added = [];

        public Task<ConversationSession?> GetBySessionKeyAsync(Guid sessionKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Added.FirstOrDefault(x => x.SessionKey == sessionKey));
        }

        public Task<ConversationSession?> GetLatestAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Latest);
        }

        public Task AddAsync(ConversationSession session, CancellationToken cancellationToken = default)
        {
            if (session.Id == 0)
            {
                session.Id = Added.Count + 1;
            }
            Added.Add(session);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeConversationHistoryRepository : IConversationHistoryRepository
    {
        public bool ThrowOnAdd { get; set; }
        public readonly List<ConversationHistoryEntry> Added = [];
        public readonly Dictionary<int, List<ConversationHistoryEntry>> Seeded = [];

        public Task AddAsync(ConversationHistoryEntry entry, CancellationToken cancellationToken = default)
        {
            if (ThrowOnAdd)
            {
                throw new InvalidOperationException("history add failed");
            }

            Added.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ConversationHistoryEntry>> GetRecentBySessionIdAsync(int sessionId, int take, CancellationToken cancellationToken = default)
        {
            if (!Seeded.TryGetValue(sessionId, out var list))
            {
                return Task.FromResult<IReadOnlyList<ConversationHistoryEntry>>([]);
            }

            var result = list.OrderByDescending(x => x.SentAtUtc).Take(take).OrderBy(x => x.SentAtUtc).ToList();
            return Task.FromResult<IReadOnlyList<ConversationHistoryEntry>>(result);
        }
    }

    private sealed class FakeUserMemoryRepository : IUserMemoryRepository
    {
        public readonly List<UserMemoryEntry> Active = [];

        public Task<UserMemoryEntry?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Active.FirstOrDefault(x => x.Key == key));
        }

        public Task<IReadOnlyList<UserMemoryEntry>> GetActiveAsync(int take, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<UserMemoryEntry>>(Active.Where(x => x.IsActive).Take(take).ToList());
        }

        public Task AddAsync(UserMemoryEntry entry, CancellationToken cancellationToken = default)
        {
            if (entry.Id == 0)
            {
                entry.Id = Active.Count + 1;
            }

            Active.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeConversationMemoryExtractor : IConversationMemoryExtractor
    {
        public IReadOnlyCollection<MemoryFactCandidate> Facts { get; set; } = [];

        public IReadOnlyCollection<MemoryFactCandidate> ExtractFromUserMessage(string userMessage)
        {
            return Facts;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int BeginCount { get; private set; }
        public int SaveCount { get; private set; }
        public int CommitCount { get; private set; }
        public int RollbackCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            BeginCount++;
            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            RollbackCount++;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}


