namespace LagerthaAssistant.Application.Tests.Services.Vocabulary;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;
using LagerthaAssistant.Domain.Entities;
using Xunit;

public sealed class VocabularySaveModePreferenceServiceTests
{
    [Fact]
    public async Task GetModeAsync_ShouldReturnScopedStoredMode_WhenExists()
    {
        var repo = new FakeUserMemoryRepository();
        repo.SetEntry(UserPreferenceMemoryKeys.SaveMode, "auto", "api", "mike");

        var sut = new VocabularySaveModePreferenceService(repo, new FakeUnitOfWork());

        var result = await sut.GetModeAsync(ConversationScope.Create("api", "mike", "chat-1"), CancellationToken.None);

        Assert.Equal(VocabularySaveMode.Auto, result);
    }

    [Fact]
    public async Task GetModeAsync_ShouldReturnAsk_WhenNoStoredValue()
    {
        var repo = new FakeUserMemoryRepository();
        var sut = new VocabularySaveModePreferenceService(repo, new FakeUnitOfWork());

        var result = await sut.GetModeAsync(ConversationScope.Create("api", "mike", "chat-1"), CancellationToken.None);

        Assert.Equal(VocabularySaveMode.Ask, result);
    }

    [Fact]
    public async Task SetModeAsync_ShouldPersistEntry_ForScope()
    {
        var repo = new FakeUserMemoryRepository();
        var unitOfWork = new FakeUnitOfWork();
        var sut = new VocabularySaveModePreferenceService(repo, unitOfWork);

        var scope = ConversationScope.Create("telegram", "mike", "chat-42");
        var result = await sut.SetModeAsync(scope, VocabularySaveMode.Off, CancellationToken.None);

        Assert.Equal(VocabularySaveMode.Off, result);
        Assert.Equal(1, unitOfWork.SaveChangesCalls);

        var entry = await repo.GetByKeyAsync(UserPreferenceMemoryKeys.SaveMode, "telegram", "mike", CancellationToken.None);
        Assert.NotNull(entry);
        Assert.Equal("off", entry!.Value);
        Assert.False(entry.IsActive);
    }

    [Theory]
    [InlineData("ask", VocabularySaveMode.Ask)]
    [InlineData("auto", VocabularySaveMode.Auto)]
    [InlineData("off", VocabularySaveMode.Off)]
    [InlineData(" ASK ", VocabularySaveMode.Ask)]
    public void TryParse_ShouldSupportKnownValues(string value, VocabularySaveMode expected)
    {
        var sut = new VocabularySaveModePreferenceService(new FakeUserMemoryRepository(), new FakeUnitOfWork());

        var success = sut.TryParse(value, out var mode);

        Assert.True(success);
        Assert.Equal(expected, mode);
    }

    [Fact]
    public void TryParse_ShouldFailForUnknownValue()
    {
        var sut = new VocabularySaveModePreferenceService(new FakeUserMemoryRepository(), new FakeUnitOfWork());

        var success = sut.TryParse("cloud", out var mode);

        Assert.False(success);
        Assert.Equal(VocabularySaveMode.Ask, mode);
    }

    [Fact]
    public void SupportedModes_ShouldReflectSaveModeEnumValues()
    {
        var sut = new VocabularySaveModePreferenceService(new FakeUserMemoryRepository(), new FakeUnitOfWork());

        var expected = Enum
            .GetValues<VocabularySaveMode>()
            .Select(sut.ToText)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Equal(expected, sut.SupportedModes);
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
}
