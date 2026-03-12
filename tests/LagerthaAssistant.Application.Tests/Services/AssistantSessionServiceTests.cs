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
    public async Task GetSystemPromptAsync_ShouldReturnActivePromptFromRepository()
    {
        var fx = new Fixture();
        fx.SystemPromptRepo.ActivePrompt = new SystemPromptEntry
        {
            PromptText = "Persisted prompt",
            Version = 7,
            IsActive = true,
            Source = "manual",
            CreatedAtUtc = fx.Clock.UtcNow
        };

        var sut = fx.CreateSut();

        var prompt = await sut.GetSystemPromptAsync();

        Assert.Equal("Persisted prompt", prompt);
    }

    [Fact]
    public async Task SetSystemPromptAsync_ShouldCreateNewActiveVersion()
    {
        var fx = new Fixture();
        fx.SystemPromptRepo.Entries.Clear();

        var current = new SystemPromptEntry
        {
            Id = 1,
            PromptText = "Old prompt",
            Version = 1,
            IsActive = true,
            Source = "seed",
            CreatedAtUtc = fx.Clock.UtcNow.AddMinutes(-10)
        };
        fx.SystemPromptRepo.Entries.Add(current);
        fx.SystemPromptRepo.ActivePrompt = current;

        var sut = fx.CreateSut();

        var updated = await sut.SetSystemPromptAsync("New prompt text", "manual");

        Assert.Equal("New prompt text", updated);
        Assert.False(current.IsActive);
        Assert.Equal(2, fx.SystemPromptRepo.Entries.Count);

        var active = Assert.Single(fx.SystemPromptRepo.Entries, x => x.IsActive);
        Assert.Equal(2, active.Version);
        Assert.Equal("manual", active.Source);
    }

    [Fact]
    public async Task AskAsync_ShouldNormalizeIrregularVerbReply_WhenModelReturnsVAndTooFewExamples()
    {
        var fx = new Fixture();
        fx.AiClient.NextContent = """
undertake - undertook - undertaken

(v) ??????? ?? ????, ???????? ??????????

The developer undertook the task of refactoring the legacy code.

She has undertaken to deliver the project by the end of the month.
""";

        var sut = fx.CreateSut();

        var result = await sut.AskAsync("undertake");

        Assert.StartsWith("undertake - undertook - undertaken", result.Content, StringComparison.Ordinal);
        Assert.Contains("(iv) ??????? ?? ????, ???????? ??????????", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("(v) ??????? ?? ????", result.Content, StringComparison.Ordinal);

        var nonEmptyLines = result.Content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith("(", StringComparison.Ordinal))
            .Skip(1)
            .ToList();

        Assert.Equal(3, nonEmptyLines.Count);
    }

    [Fact]
    public async Task AskAsync_ShouldNormalizeRegularVerbReply_WhenModelReturnsIvForSingleFormWord()
    {
        var fx = new Fixture();
        fx.AiClient.NextContent = """
prepare

(iv) get ready, make something ready

We prepare the deployment scripts before the release.
""";

        var sut = fx.CreateSut();

        var result = await sut.AskAsync("prepare");

        Assert.Contains("(v) get ready, make something ready", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("(iv) get ready, make something ready", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AskAsync_ShouldNormalizePersistentExpressionReply_AndNotTreatItAsPhrasalVerb()
    {
        var fx = new Fixture();
        fx.AiClient.NextContent = """
on the same page

(v) мати спільне розуміння

We are on the same page about the release.
""";

        var sut = fx.CreateSut();

        var result = await sut.AskAsync("on the same page");

        Assert.StartsWith("On the same page", result.Content, StringComparison.Ordinal);
        Assert.Contains("(pe) мати спільне розуміння", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("(pv)", result.Content, StringComparison.OrdinalIgnoreCase);

        var nonEmptyLines = result.Content
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        Assert.Equal(2, nonEmptyLines.Count);
    }
    [Fact]
    public async Task AskAsync_ShouldNormalizePhrasalVerbReply_WhenModelReturnsV()
    {
        var fx = new Fixture();
        fx.AiClient.NextContent = """
call back

(v) call in return, phone someone in response

Please call back the client after the meeting.
""";

        var sut = fx.CreateSut();

        var result = await sut.AskAsync("call back");

        Assert.StartsWith("call back", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("(pv) call in return, phone someone in response", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("(v) call in return, phone someone in response", result.Content, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task GenerateSystemPromptProposalAsync_ShouldSaveAssistantProposal()
    {
        var fx = new Fixture();
        fx.AiClient.NextContent = "You are a better prompt.";
        var sut = fx.CreateSut();

        var proposal = await sut.GenerateSystemPromptProposalAsync("be stricter with safety");

        Assert.Equal("assistant", proposal.Source);
        Assert.Equal(SystemPromptProposalStatuses.Pending, proposal.Status);
        Assert.Equal("You are a better prompt.", proposal.ProposedPrompt);
        Assert.Contains(fx.ProposalRepo.Proposals, x => x.Id == proposal.Id);
    }

    [Fact]
    public async Task ApplySystemPromptProposalAsync_ShouldApplyAndMarkProposal()
    {
        var fx = new Fixture();
        var proposal = new SystemPromptProposal
        {
            Id = 12,
            ProposedPrompt = "Applied prompt",
            Reason = "Test",
            Confidence = 0.9,
            Source = "assistant",
            Status = SystemPromptProposalStatuses.Pending,
            CreatedAtUtc = fx.Clock.UtcNow
        };
        fx.ProposalRepo.Proposals.Add(proposal);

        var sut = fx.CreateSut();

        var applied = await sut.ApplySystemPromptProposalAsync(12);

        Assert.Equal("Applied prompt", applied);
        Assert.Equal(SystemPromptProposalStatuses.Applied, proposal.Status);
        Assert.NotNull(proposal.ReviewedAtUtc);
        Assert.True(proposal.AppliedSystemPromptEntryId.HasValue);
    }

    private sealed class Fixture
    {
        public FakeAiChatClient AiClient { get; } = new();
        public FakeConversationSessionRepository SessionRepo { get; } = new();
        public FakeConversationHistoryRepository HistoryRepo { get; } = new();
        public FakeUserMemoryRepository MemoryRepo { get; } = new();
        public FakeSystemPromptRepository SystemPromptRepo { get; } = new();
        public FakeSystemPromptProposalRepository ProposalRepo { get; } = new();
        public FakeConversationMemoryExtractor MemoryExtractor { get; } = new();
        public FakeUnitOfWork Uow { get; } = new();
        public FakeClock Clock { get; } = new();

        public Fixture()
        {
            var seed = new SystemPromptEntry
            {
                Id = 1,
                PromptText = "system prompt",
                Version = 1,
                IsActive = true,
                Source = "seed",
                CreatedAtUtc = Clock.UtcNow
            };
            SystemPromptRepo.Entries.Add(seed);
            SystemPromptRepo.ActivePrompt = seed;
        }

        public AssistantSessionService CreateSut()
        {
            return new AssistantSessionService(
                AiClient,
                SessionRepo,
                HistoryRepo,
                MemoryRepo,
                SystemPromptRepo,
                ProposalRepo,
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
        public string NextContent { get; set; } = "ok";
        public IReadOnlyCollection<ConversationMessage>? LastMessages { get; private set; }

        public Task<AssistantCompletionResult> CompleteAsync(IReadOnlyCollection<ConversationMessage> messages, CancellationToken cancellationToken = default)
        {
            LastMessages = messages;
            return Task.FromResult(new AssistantCompletionResult(NextContent, "test-model", null));
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

    private sealed class FakeSystemPromptRepository : ISystemPromptRepository
    {
        public readonly List<SystemPromptEntry> Entries = [];
        public SystemPromptEntry? ActivePrompt { get; set; }

        public Task<SystemPromptEntry?> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ActivePrompt);
        }

        public Task<IReadOnlyList<SystemPromptEntry>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
        {
            var result = Entries.OrderByDescending(x => x.Version).Take(Math.Max(0, take)).ToList();
            return Task.FromResult<IReadOnlyList<SystemPromptEntry>>(result);
        }

        public Task<int> GetLatestVersionAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Entries.Count == 0 ? 0 : Entries.Max(x => x.Version));
        }

        public Task AddAsync(SystemPromptEntry entry, CancellationToken cancellationToken = default)
        {
            if (entry.Id == 0)
            {
                entry.Id = Entries.Count + 1;
            }

            Entries.Add(entry);
            if (entry.IsActive)
            {
                ActivePrompt = entry;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeSystemPromptProposalRepository : ISystemPromptProposalRepository
    {
        public readonly List<SystemPromptProposal> Proposals = [];

        public Task<SystemPromptProposal?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Proposals.FirstOrDefault(x => x.Id == id));
        }

        public Task<IReadOnlyList<SystemPromptProposal>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
        {
            var result = Proposals.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Max(0, take)).ToList();
            return Task.FromResult<IReadOnlyList<SystemPromptProposal>>(result);
        }

        public Task AddAsync(SystemPromptProposal proposal, CancellationToken cancellationToken = default)
        {
            if (proposal.Id == 0)
            {
                proposal.Id = Proposals.Count + 1;
            }

            Proposals.Add(proposal);
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




