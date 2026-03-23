namespace LagerthaAssistant.Application.Tests.Services;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Services;
using LagerthaAssistant.Domain.Abstractions;
using LagerthaAssistant.Domain.Entities;
using Xunit;

public sealed class LocaleDetectionTests
{
    [Theory]
    [InlineData("ru", "Привет", LocalizationConstants.UkrainianLocale)]
    [InlineData("ru-RU", "Привет", LocalizationConstants.UkrainianLocale)]
    [InlineData("uk", "Привіт", LocalizationConstants.UkrainianLocale)]
    [InlineData("en", "Hello", LocalizationConstants.EnglishLocale)]
    [InlineData(null, "Hello", LocalizationConstants.EnglishLocale)]
    public async Task EnsureLocaleAsync_ShouldInitializeExpectedLocale(string? telegramLanguageCode, string text, string expectedLocale)
    {
        var memoryRepo = new TestUserMemoryRepository();
        var sut = CreateSut(memoryRepo);

        var result = await sut.EnsureLocaleAsync("telegram", "user-1", telegramLanguageCode, text);

        Assert.Equal(expectedLocale, result.Locale);
        var stored = Assert.Single(memoryRepo.Entries);
        Assert.Equal(expectedLocale, stored.Value);
    }

    [Theory]
    [InlineData("Привіт")]
    [InlineData("Їжа")]
    [InlineData("Єва")]
    public async Task EnsureLocaleAsync_ShouldSwitchToUkrainian_AfterTwoUkrainianSpecificMessages(string text)
    {
        var memoryRepo = new TestUserMemoryRepository();
        memoryRepo.Entries.Add(CreateLocaleEntry(LocalizationConstants.EnglishLocale, 1.0));
        var sut = CreateSut(memoryRepo);

        var first = await sut.EnsureLocaleAsync("telegram", "user-1", "en", text);
        var second = await sut.EnsureLocaleAsync("telegram", "user-1", "en", text);

        Assert.Equal(LocalizationConstants.EnglishLocale, first.Locale);
        Assert.False(first.IsSwitched);
        Assert.Equal(LocalizationConstants.UkrainianLocale, second.Locale);
        Assert.True(second.IsSwitched);
    }

    [Theory]
    [InlineData("Привет")]
    [InlineData("")]
    [InlineData(null)]
    public async Task EnsureLocaleAsync_ShouldNotSwitch_WhenTextIsNotUkrainianSpecific(string? text)
    {
        var memoryRepo = new TestUserMemoryRepository();
        memoryRepo.Entries.Add(CreateLocaleEntry(LocalizationConstants.EnglishLocale, 1.0));
        var sut = CreateSut(memoryRepo);

        var result = await sut.EnsureLocaleAsync("telegram", "user-1", "en", text);

        Assert.Equal(LocalizationConstants.EnglishLocale, result.Locale);
        Assert.False(result.IsSwitched);
        var stored = Assert.Single(memoryRepo.Entries);
        Assert.Equal(LocalizationConstants.EnglishLocale, stored.Value);
    }

    private static UserMemoryEntry CreateLocaleEntry(string locale, double confidence)
    {
        return new UserMemoryEntry
        {
            Channel = "telegram",
            UserId = "user-1",
            Key = LocalizationConstants.LocaleMemoryKey,
            Value = locale,
            Confidence = confidence,
            IsActive = true,
            LastSeenAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static UserLocaleStateService CreateSut(TestUserMemoryRepository memoryRepo)
    {
        return new UserLocaleStateService(
            memoryRepo,
            new TestUnitOfWork(),
            new TestLocalizationService(),
            new TestClock());
    }

    private sealed class TestLocalizationService : ILocalizationService
    {
        public string Get(string key, string locale) => key;

        public string GetLocaleForUser(string? telegramLanguageCode)
        {
            if (telegramLanguageCode?.StartsWith("ru", StringComparison.OrdinalIgnoreCase) == true
                || telegramLanguageCode?.StartsWith(LocalizationConstants.UkrainianLocale, StringComparison.OrdinalIgnoreCase) == true)
            {
                return LocalizationConstants.UkrainianLocale;
            }

            return LocalizationConstants.EnglishLocale;
        }
    }

    private sealed class TestUserMemoryRepository : IUserMemoryRepository
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

    private sealed class TestUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 3, 18, 12, 0, 0, TimeSpan.Zero);
    }
}
