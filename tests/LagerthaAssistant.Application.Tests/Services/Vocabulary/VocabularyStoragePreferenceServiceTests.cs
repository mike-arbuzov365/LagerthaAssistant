namespace LagerthaAssistant.Application.Tests.Services.Vocabulary;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;
using LagerthaAssistant.Domain.Entities;
using Xunit;

public sealed class VocabularyStoragePreferenceServiceTests
{
    [Fact]
    public async Task GetModeAsync_ShouldReturnScopedStoredMode_WhenExists()
    {
        var repo = new FakeUserMemoryRepository();
        repo.SetEntry(UserPreferenceMemoryKeys.StorageMode, "graph", "api", "mike");

        var modeProvider = new FakeStorageModeProvider(VocabularyStorageMode.Local);
        var sut = new VocabularyStoragePreferenceService(repo, new FakeUnitOfWork(), modeProvider);

        var result = await sut.GetModeAsync(ConversationScope.Create("api", "mike", "chat-1"), CancellationToken.None);

        Assert.Equal(VocabularyStorageMode.Graph, result);
    }

    [Fact]
    public async Task GetModeAsync_ShouldFallbackToProviderCurrentMode_WhenNoStoredValue()
    {
        var repo = new FakeUserMemoryRepository();
        var modeProvider = new FakeStorageModeProvider(VocabularyStorageMode.Local);
        var sut = new VocabularyStoragePreferenceService(repo, new FakeUnitOfWork(), modeProvider);

        var result = await sut.GetModeAsync(ConversationScope.Create("api", "mike", "chat-1"), CancellationToken.None);

        Assert.Equal(VocabularyStorageMode.Local, result);
    }

    [Fact]
    public async Task SetModeAsync_ShouldPersistEntry_ForScope()
    {
        var repo = new FakeUserMemoryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var modeProvider = new FakeStorageModeProvider(VocabularyStorageMode.Local);
        var sut = new VocabularyStoragePreferenceService(repo, unitOfWork, modeProvider);

        var scope = ConversationScope.Create("telegram", "mike", "chat-42");
        var result = await sut.SetModeAsync(scope, VocabularyStorageMode.Graph, CancellationToken.None);

        Assert.Equal(VocabularyStorageMode.Graph, result);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);

        var entry = await repo.GetByKeyAsync(UserPreferenceMemoryKeys.StorageMode, "telegram", "mike", CancellationToken.None);
        Assert.NotNull(entry);
        Assert.Equal("graph", entry!.Value);
        Assert.False(entry.IsActive);
    }

    private sealed class FakeUserMemoryRepository : IUserMemoryRepository
    {
        private readonly Dictionary<(string Key, string Channel, string UserId), UserMemoryEntry> _entries = new();

        public void SetEntry(string key, string value, string channel, string userId)
        {
            _entries[(key, channel, userId)] = new UserMemoryEntry
            {
                Key = key,
                Value = value,
                Channel = channel,
                UserId = userId,
                IsActive = false,
                Confidence = 1.0,
                LastSeenAtUtc = DateTimeOffset.UtcNow
            };
        }

        public Task<UserMemoryEntry?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        {
            return GetByKeyAsync(key, ConversationScope.DefaultChannel, ConversationScope.DefaultUserId, cancellationToken);
        }

        public Task<UserMemoryEntry?> GetByKeyAsync(
            string key,
            string channel,
            string userId,
            CancellationToken cancellationToken = default)
        {
            _entries.TryGetValue((key, channel, userId), out var entry);
            return Task.FromResult(entry);
        }

        public Task<IReadOnlyList<UserMemoryEntry>> GetActiveAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserMemoryEntry>>([]);

        public Task<IReadOnlyList<UserMemoryEntry>> GetActiveAsync(
            int take,
            string channel,
            string userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserMemoryEntry>>([]);

        public Task AddAsync(UserMemoryEntry entry, CancellationToken cancellationToken = default)
        {
            _entries[(entry.Key, entry.Channel, entry.UserId)] = entry;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class FakeStorageModeProvider : IVocabularyStorageModeProvider
    {
        public FakeStorageModeProvider(VocabularyStorageMode currentMode)
        {
            CurrentMode = currentMode;
        }

        public VocabularyStorageMode CurrentMode { get; private set; }

        public void SetMode(VocabularyStorageMode mode)
        {
            CurrentMode = mode;
        }

        public bool TryParse(string? value, out VocabularyStorageMode mode)
        {
            if (string.Equals(value, "local", StringComparison.OrdinalIgnoreCase))
            {
                mode = VocabularyStorageMode.Local;
                return true;
            }

            if (string.Equals(value, "graph", StringComparison.OrdinalIgnoreCase))
            {
                mode = VocabularyStorageMode.Graph;
                return true;
            }

            mode = VocabularyStorageMode.Local;
            return false;
        }

        public string ToText(VocabularyStorageMode mode)
            => mode.ToString().ToLowerInvariant();
    }
}
