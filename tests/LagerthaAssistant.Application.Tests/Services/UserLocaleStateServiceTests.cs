namespace LagerthaAssistant.Application.Tests.Services;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Services;
using LagerthaAssistant.Domain.Abstractions;
using LagerthaAssistant.Domain.Entities;
using Xunit;

public sealed class UserLocaleStateServiceTests
{
    [Fact]
    public async Task EnsureLocaleAsync_ShouldInitializeLocaleFromTelegramLanguageCode()
    {
        var memoryRepo = new FakeUserMemoryRepository();
        var sut = new UserLocaleStateService(
            memoryRepo,
            new FakeUnitOfWork(),
            new FakeLocalizationService(),
            new FakeClock());

        var result = await sut.EnsureLocaleAsync("telegram", "user-1", "ru", "hello");

        Assert.True(result.IsInitialized);
        Assert.Equal(LocalizationConstants.UkrainianLocale, result.Locale);
        var entry = Assert.Single(memoryRepo.Entries);
        Assert.Equal(LocalizationConstants.LocaleMemoryKey, entry.Key);
        Assert.Equal(LocalizationConstants.UkrainianLocale, entry.Value);
    }

    [Fact]
    public async Task EnsureLocaleAsync_ShouldSwitchAfterTwoConsecutiveMessagesInNewLanguage()
    {
        var memoryRepo = new FakeUserMemoryRepository();
        memoryRepo.Entries.Add(new UserMemoryEntry
        {
            Channel = "telegram",
            UserId = "user-1",
            Key = LocalizationConstants.LocaleMemoryKey,
            Value = LocalizationConstants.EnglishLocale,
            Confidence = 1.0,
            IsActive = true,
            LastSeenAtUtc = DateTimeOffset.UtcNow
        });

        var sut = new UserLocaleStateService(
            memoryRepo,
            new FakeUnitOfWork(),
            new FakeLocalizationService(),
            new FakeClock());

        var first = await sut.EnsureLocaleAsync("telegram", "user-1", "en", "привіт");
        Assert.False(first.IsSwitched);
        Assert.Equal(LocalizationConstants.EnglishLocale, first.Locale);

        var second = await sut.EnsureLocaleAsync("telegram", "user-1", "en", "це український текст");
        Assert.True(second.IsSwitched);
        Assert.Equal(LocalizationConstants.UkrainianLocale, second.Locale);

        var localeEntry = Assert.Single(memoryRepo.Entries);
        Assert.Equal(LocalizationConstants.UkrainianLocale, localeEntry.Value);
        Assert.Equal(1.0, localeEntry.Confidence);
    }

    private sealed class FakeLocalizationService : ILocalizationService
    {
        public string Get(string key, string locale) => key;

        public string GetLocaleForUser(string? telegramLanguageCode)
        {
            if (telegramLanguageCode?.StartsWith("ru", StringComparison.OrdinalIgnoreCase) == true)
            {
                return LocalizationConstants.UkrainianLocale;
            }

            if (telegramLanguageCode?.StartsWith("uk", StringComparison.OrdinalIgnoreCase) == true)
            {
                return LocalizationConstants.UkrainianLocale;
            }

            return LocalizationConstants.EnglishLocale;
        }

        public bool IsRussian(string? languageCode)
            => languageCode?.StartsWith("ru", StringComparison.OrdinalIgnoreCase) == true;
    }

    private sealed class FakeUserMemoryRepository : IUserMemoryRepository
    {
        public List<UserMemoryEntry> Entries { get; } = [];

        public Task<UserMemoryEntry?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
            => GetByKeyAsync(key, "unknown", "anonymous", cancellationToken);

        public Task<UserMemoryEntry?> GetByKeyAsync(string key, string channel, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Entries.FirstOrDefault(x => x.Key == key && x.Channel == channel && x.UserId == userId));

        public Task<IReadOnlyList<UserMemoryEntry>> GetActiveAsync(int take, CancellationToken cancellationToken = default)
            => GetActiveAsync(take, "unknown", "anonymous", cancellationToken);

        public Task<IReadOnlyList<UserMemoryEntry>> GetActiveAsync(int take, string channel, string userId, CancellationToken cancellationToken = default)
        {
            var items = Entries
                .Where(x => x.IsActive && x.Channel == channel && x.UserId == userId)
                .Take(take)
                .ToList();
            return Task.FromResult<IReadOnlyList<UserMemoryEntry>>(items);
        }

        public Task AddAsync(UserMemoryEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 3, 18, 12, 0, 0, TimeSpan.Zero);
    }
}
