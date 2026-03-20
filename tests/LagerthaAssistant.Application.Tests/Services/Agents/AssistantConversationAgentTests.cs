namespace LagerthaAssistant.Application.Tests.Services.Agents;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Localization;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Agents;
using LagerthaAssistant.Domain.AI;
using LagerthaAssistant.Domain.Entities;
using Xunit;

public sealed class AssistantConversationAgentTests
{
    [Fact]
    public async Task HandleAsync_ShouldChangeSaveMode_AndReturnSettingsProofByDefault()
    {
        var fx = new Fixture();
        fx.Memory.Seed(fx.Scope.Channel, fx.Scope.UserId, LocalizationConstants.LocaleMemoryKey, "uk");
        var sut = fx.CreateSut();

        var result = await sut.HandleAsync(
            new ConversationAgentContext(
                $"{ConversationInputMarkers.Chat} зміни спосіб збереження на авто",
                [$"{ConversationInputMarkers.Chat} зміни спосіб збереження на авто"],
                fx.Scope),
            CancellationToken.None);

        Assert.Equal("assistant.settings.save_mode.updated", result.Intent);
        Assert.Contains("Режим збереження змінено на auto", result.Message, StringComparison.Ordinal);
        Assert.Contains("Поточні налаштування", result.Message, StringComparison.Ordinal);
        Assert.Equal(VocabularySaveMode.Auto, fx.SaveModes.Get(fx.Scope));
    }

    [Fact]
    public async Task HandleAsync_ShouldRememberProofPreference_AndUseShortConfirmation()
    {
        var fx = new Fixture();
        fx.Memory.Seed(fx.Scope.Channel, fx.Scope.UserId, LocalizationConstants.LocaleMemoryKey, "uk");
        var sut = fx.CreateSut();

        await sut.HandleAsync(
            new ConversationAgentContext(
                $"{ConversationInputMarkers.Chat} запам'ятай: після зміни не треба показувати налаштування",
                [$"{ConversationInputMarkers.Chat} запам'ятай: після зміни не треба показувати налаштування"],
                fx.Scope),
            CancellationToken.None);

        var result = await sut.HandleAsync(
            new ConversationAgentContext(
                $"{ConversationInputMarkers.Chat} зміни спосіб збереження на авто",
                [$"{ConversationInputMarkers.Chat} зміни спосіб збереження на авто"],
                fx.Scope),
            CancellationToken.None);

        Assert.Equal("assistant.settings.save_mode.updated", result.Intent);
        Assert.Contains("Режим збереження змінено на auto", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Поточні налаштування", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_ShouldUseAiChatFallback_ForGeneralChatInput()
    {
        var fx = new Fixture();
        fx.Memory.Seed(fx.Scope.Channel, fx.Scope.UserId, LocalizationConstants.LocaleMemoryKey, "en");
        fx.Ai.NextContent = "Here is a free-form assistant reply.";
        var sut = fx.CreateSut();

        var result = await sut.HandleAsync(
            new ConversationAgentContext(
                $"{ConversationInputMarkers.Chat} explain this architecture",
                [$"{ConversationInputMarkers.Chat} explain this architecture"],
                fx.Scope),
            CancellationToken.None);

        Assert.Equal("assistant.chat", result.Intent);
        Assert.Equal("Here is a free-form assistant reply.", result.Message);
        Assert.True(fx.Ai.Calls > 0);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnPartOfSpeechCount_ForDirectQuestion()
    {
        var fx = new Fixture();
        fx.Memory.Seed(fx.Scope.Channel, fx.Scope.UserId, LocalizationConstants.LocaleMemoryKey, "uk");
        var sut = fx.CreateSut();

        var result = await sut.HandleAsync(
            new ConversationAgentContext(
                $"{ConversationInputMarkers.Chat} Скажи, скільки дієслів у нашому словнику?",
                [$"{ConversationInputMarkers.Chat} Скажи, скільки дієслів у нашому словнику?"],
                fx.Scope),
            CancellationToken.None);

        Assert.Equal("assistant.vocabulary.stats.part_of_speech", result.Intent);
        Assert.Contains("53", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_ShouldRecognizeStatsIntent_ByNaturalUkrainianPhrase()
    {
        var fx = new Fixture();
        fx.Memory.Seed(fx.Scope.Channel, fx.Scope.UserId, LocalizationConstants.LocaleMemoryKey, "uk");
        var sut = fx.CreateSut();

        var result = await sut.HandleAsync(
            new ConversationAgentContext(
                $"{ConversationInputMarkers.Chat} А якщо подивитись у статистику?",
                [$"{ConversationInputMarkers.Chat} А якщо подивитись у статистику?"],
                fx.Scope),
            CancellationToken.None);

        Assert.Equal("assistant.vocabulary.stats", result.Intent);
        Assert.Contains("Статистика словника", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_ShouldChangeLocale_WhenUserAsksToSwitchLanguage()
    {
        var fx = new Fixture();
        fx.Memory.Seed(fx.Scope.Channel, fx.Scope.UserId, LocalizationConstants.LocaleMemoryKey, "uk");
        var sut = fx.CreateSut();

        var result = await sut.HandleAsync(
            new ConversationAgentContext(
                $"{ConversationInputMarkers.Chat} Переключи мову на англійську",
                [$"{ConversationInputMarkers.Chat} Переключи мову на англійську"],
                fx.Scope),
            CancellationToken.None);

        Assert.Equal("assistant.settings.language.updated", result.Intent);
        Assert.Equal(LocalizationConstants.EnglishLocale, fx.LocaleState.LastSetLocale);
        Assert.Contains("Language changed", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_ShouldStartVocabularyAddFlow_WhenUserAsksToAddWord()
    {
        var fx = new Fixture();
        fx.Memory.Seed(fx.Scope.Channel, fx.Scope.UserId, LocalizationConstants.LocaleMemoryKey, "uk");
        var sut = fx.CreateSut();

        var result = await sut.HandleAsync(
            new ConversationAgentContext(
                $"{ConversationInputMarkers.Chat} Додай слово у словник",
                [$"{ConversationInputMarkers.Chat} Додай слово у словник"],
                fx.Scope),
            CancellationToken.None);

        Assert.Equal("assistant.vocabulary.add.start", result.Intent);
        Assert.Contains("надішліть слово", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnCapabilities_ForFullActionsQuestion()
    {
        var fx = new Fixture();
        fx.Memory.Seed(fx.Scope.Channel, fx.Scope.UserId, LocalizationConstants.LocaleMemoryKey, "uk");
        var sut = fx.CreateSut();

        var result = await sut.HandleAsync(
            new ConversationAgentContext(
                $"{ConversationInputMarkers.Chat} Які дії ти можеш робити? Повний перелік",
                [$"{ConversationInputMarkers.Chat} Які дії ти можеш робити? Повний перелік"],
                fx.Scope),
            CancellationToken.None);

        Assert.Equal("assistant.capabilities", result.Intent);
        Assert.Contains("Я можу допомогти", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_ShouldOpenSettings_WhenUserAsksNaturally()
    {
        var fx = new Fixture();
        fx.Memory.Seed(fx.Scope.Channel, fx.Scope.UserId, LocalizationConstants.LocaleMemoryKey, "uk");
        var sut = fx.CreateSut();

        var result = await sut.HandleAsync(
            new ConversationAgentContext(
                $"{ConversationInputMarkers.Chat} Відкрий налаштування",
                [$"{ConversationInputMarkers.Chat} Відкрий налаштування"],
                fx.Scope),
            CancellationToken.None);

        Assert.Equal("assistant.settings.open", result.Intent);
    }

    [Fact]
    public async Task HandleAsync_ShouldStartImportPhotoFlow_WhenUserAsksNaturally()
    {
        var fx = new Fixture();
        fx.Memory.Seed(fx.Scope.Channel, fx.Scope.UserId, LocalizationConstants.LocaleMemoryKey, "uk");
        var sut = fx.CreateSut();

        var result = await sut.HandleAsync(
            new ConversationAgentContext(
                $"{ConversationInputMarkers.Chat} Скину тобі фото, перевір нові слова",
                [$"{ConversationInputMarkers.Chat} Скину тобі фото, перевір нові слова"],
                fx.Scope),
            CancellationToken.None);

        Assert.Equal("assistant.vocabulary.import.source.photo", result.Intent);
    }

    [Fact]
    public async Task HandleAsync_ShouldStartOneDriveLogin_WhenUserAsksNaturally()
    {
        var fx = new Fixture();
        fx.Memory.Seed(fx.Scope.Channel, fx.Scope.UserId, LocalizationConstants.LocaleMemoryKey, "uk");
        var sut = fx.CreateSut();

        var result = await sut.HandleAsync(
            new ConversationAgentContext(
                $"{ConversationInputMarkers.Chat} Увійди в OneDrive",
                [$"{ConversationInputMarkers.Chat} Увійди в OneDrive"],
                fx.Scope),
            CancellationToken.None);

        Assert.Equal("assistant.onedrive.login", result.Intent);
    }

    [Fact]
    public async Task HandleAsync_ShouldOpenLanguagePanel_WhenUserAsksNaturally()
    {
        var fx = new Fixture();
        fx.Memory.Seed(fx.Scope.Channel, fx.Scope.UserId, LocalizationConstants.LocaleMemoryKey, "uk");
        var sut = fx.CreateSut();

        var result = await sut.HandleAsync(
            new ConversationAgentContext(
                $"{ConversationInputMarkers.Chat} Покажи мови",
                [$"{ConversationInputMarkers.Chat} Покажи мови"],
                fx.Scope),
            CancellationToken.None);

        Assert.Equal("assistant.settings.language.open", result.Intent);
    }

    [Fact]
    public async Task HandleAsync_ShouldOpenNotionPanel_WhenUserAsksNaturally()
    {
        var fx = new Fixture();
        fx.Memory.Seed(fx.Scope.Channel, fx.Scope.UserId, LocalizationConstants.LocaleMemoryKey, "uk");
        var sut = fx.CreateSut();

        var result = await sut.HandleAsync(
            new ConversationAgentContext(
                $"{ConversationInputMarkers.Chat} Відкрий Notion",
                [$"{ConversationInputMarkers.Chat} Відкрий Notion"],
                fx.Scope),
            CancellationToken.None);

        Assert.Equal("assistant.settings.notion.open", result.Intent);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Scope = ConversationScope.Create("telegram", "mike", "chat-42");
        }

        public ConversationScope Scope { get; }

        public FakeAiChatClient Ai { get; } = new();

        public FakeUserMemoryRepository Memory { get; } = new();

        public FakeUnitOfWork UnitOfWork { get; } = new();

        public FakeVocabularySaveModePreferenceService SaveModes { get; } = new();

        public FakeVocabularyStoragePreferenceService StorageModes { get; } = new();

        public FakeVocabularyCardRepository Cards { get; } = new();

        public FakeUserLocaleStateService LocaleState { get; } = new();

        public AssistantConversationAgent CreateSut()
        {
            return new AssistantConversationAgent(
                Ai,
                Memory,
                UnitOfWork,
                SaveModes,
                StorageModes,
                Cards,
                LocaleState);
        }
    }

    private sealed class FakeAiChatClient : IAiChatClient
    {
        public int Calls { get; private set; }

        public string NextContent { get; set; } = "ok";

        public Task<AssistantCompletionResult> CompleteAsync(
            IReadOnlyCollection<ConversationMessage> messages,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new AssistantCompletionResult(NextContent, "test-model", null));
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }

        public Task BeginTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }

        public Task CommitTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class FakeUserMemoryRepository : IUserMemoryRepository
    {
        private readonly Dictionary<string, UserMemoryEntry> _entries = new(StringComparer.Ordinal);

        public void Seed(string channel, string userId, string key, string value)
        {
            _entries[BuildKey(channel, userId, key)] = new UserMemoryEntry
            {
                Key = key,
                Value = value,
                Confidence = 1.0,
                IsActive = true,
                LastSeenAtUtc = DateTimeOffset.UtcNow,
                Channel = channel,
                UserId = userId
            };
        }

        public Task<UserMemoryEntry?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        {
            var entry = _entries.Values.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.Ordinal));
            return Task.FromResult(entry);
        }

        public Task<UserMemoryEntry?> GetByKeyAsync(
            string key,
            string channel,
            string userId,
            CancellationToken cancellationToken = default)
        {
            _entries.TryGetValue(BuildKey(channel, userId, key), out var entry);
            return Task.FromResult(entry);
        }

        public Task<IReadOnlyList<UserMemoryEntry>> GetActiveAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserMemoryEntry>>([]);

        public Task<IReadOnlyList<UserMemoryEntry>> GetActiveAsync(
            int take,
            string channel,
            string userId,
            CancellationToken cancellationToken = default)
        {
            var result = _entries.Values
                .Where(x => x.IsActive && string.Equals(x.Channel, channel, StringComparison.Ordinal) && string.Equals(x.UserId, userId, StringComparison.Ordinal))
                .Take(Math.Max(0, take))
                .ToList();
            return Task.FromResult<IReadOnlyList<UserMemoryEntry>>(result);
        }

        public Task AddAsync(UserMemoryEntry entry, CancellationToken cancellationToken = default)
        {
            _entries[BuildKey(entry.Channel, entry.UserId, entry.Key)] = entry;
            return Task.CompletedTask;
        }

        private static string BuildKey(string channel, string userId, string key)
            => $"{channel}|{userId}|{key}";
    }

    private sealed class FakeVocabularySaveModePreferenceService : IVocabularySaveModePreferenceService
    {
        private readonly Dictionary<string, VocabularySaveMode> _values = new(StringComparer.Ordinal);

        public IReadOnlyList<string> SupportedModes => ["ask", "auto", "off"];

        public bool TryParse(string? value, out VocabularySaveMode mode)
        {
            mode = value?.Trim().ToLowerInvariant() switch
            {
                "auto" => VocabularySaveMode.Auto,
                "off" => VocabularySaveMode.Off,
                "ask" => VocabularySaveMode.Ask,
                _ => VocabularySaveMode.Ask
            };

            return value is not null && (value.Equals("auto", StringComparison.OrdinalIgnoreCase)
                || value.Equals("off", StringComparison.OrdinalIgnoreCase)
                || value.Equals("ask", StringComparison.OrdinalIgnoreCase));
        }

        public string ToText(VocabularySaveMode mode)
            => mode switch
            {
                VocabularySaveMode.Auto => "auto",
                VocabularySaveMode.Off => "off",
                _ => "ask"
            };

        public Task<VocabularySaveMode> GetModeAsync(ConversationScope scope, CancellationToken cancellationToken = default)
        {
            var key = BuildKey(scope);
            if (_values.TryGetValue(key, out var mode))
            {
                return Task.FromResult(mode);
            }

            return Task.FromResult(VocabularySaveMode.Ask);
        }

        public Task<VocabularySaveMode> SetModeAsync(
            ConversationScope scope,
            VocabularySaveMode mode,
            CancellationToken cancellationToken = default)
        {
            _values[BuildKey(scope)] = mode;
            return Task.FromResult(mode);
        }

        public VocabularySaveMode Get(ConversationScope scope)
            => _values.TryGetValue(BuildKey(scope), out var mode) ? mode : VocabularySaveMode.Ask;

        private static string BuildKey(ConversationScope scope)
            => $"{scope.Channel}|{scope.UserId}|{scope.ConversationId}";
    }

    private sealed class FakeVocabularyStoragePreferenceService : IVocabularyStoragePreferenceService
    {
        public IReadOnlyList<string> SupportedModes => ["local", "graph"];

        public Task<VocabularyStorageMode> GetModeAsync(ConversationScope scope, CancellationToken cancellationToken = default)
            => Task.FromResult(VocabularyStorageMode.Graph);

        public Task<VocabularyStorageMode> SetModeAsync(
            ConversationScope scope,
            VocabularyStorageMode mode,
            CancellationToken cancellationToken = default)
            => Task.FromResult(mode);
    }

    private sealed class FakeUserLocaleStateService : IUserLocaleStateService
    {
        public string? LastSetLocale { get; private set; }

        public Task<string?> GetStoredLocaleAsync(string channel, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<string> SetLocaleAsync(
            string channel,
            string userId,
            string locale,
            bool selectedManually,
            CancellationToken cancellationToken = default)
        {
            LastSetLocale = LocalizationConstants.NormalizeLocaleCode(locale);
            return Task.FromResult(LastSetLocale);
        }

        public Task<UserLocaleStateResult> EnsureLocaleAsync(
            string channel,
            string userId,
            string? telegramLanguageCode,
            string? incomingText,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new UserLocaleStateResult(LocalizationConstants.NormalizeLocaleCode(telegramLanguageCode), false, false));
    }

    private sealed class FakeVocabularyCardRepository : IVocabularyCardRepository
    {
        public Task<IReadOnlyList<VocabularyCard>> FindByAnyTokenAsync(IReadOnlyCollection<string> normalizedTokens, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VocabularyCard>>([]);

        public Task<VocabularyCard?> GetByIdentityAsync(string normalizedWord, string deckFileName, string storageMode, CancellationToken cancellationToken = default)
            => Task.FromResult<VocabularyCard?>(null);

        public Task AddAsync(VocabularyCard card, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> CountPendingNotionSyncAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> CountFailedNotionSyncAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<IReadOnlyList<VocabularyCard>> ClaimPendingNotionSyncAsync(int take, DateTimeOffset claimedAtUtc, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VocabularyCard>>([]);

        public Task<IReadOnlyList<VocabularyCard>> GetFailedNotionSyncAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VocabularyCard>>([]);

        public Task<int> RequeueFailedNotionSyncAsync(int take, DateTimeOffset requeuedAtUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> CountAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(123);

        public Task<IReadOnlyList<VocabularyCard>> GetRecentAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VocabularyCard>>([]);

        public Task<IReadOnlyList<VocabularyDeckStat>> GetDeckStatsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VocabularyDeckStat>>(
            [
                new VocabularyDeckStat("wm-nouns-ua-en.xlsx", 80),
                new VocabularyDeckStat("wm-verbs-us-en.xlsx", 40)
            ]);

        public Task<IReadOnlyList<VocabularyPartOfSpeechStat>> GetPartOfSpeechStatsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VocabularyPartOfSpeechStat>>(
            [
                new VocabularyPartOfSpeechStat("n", 70),
                new VocabularyPartOfSpeechStat("v", 53)
            ]);

        public Task<int> DeleteAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
