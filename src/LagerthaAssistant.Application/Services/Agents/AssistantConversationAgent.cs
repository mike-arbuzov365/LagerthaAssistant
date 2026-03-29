namespace LagerthaAssistant.Application.Services.Agents;

using System.Text;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;
using LagerthaAssistant.Domain.AI;

public sealed class AssistantConversationAgent : IConversationAgent, IConversationAgentProfile
{
    private const string ProofModeFull = "full";
    private const string ProofModeBrief = "brief";

    private static readonly string[] SaveModeTopicMarkers =
    [
        "save mode",
        "режим збереження",
        "спосіб збереження"
    ];

    private static readonly string[] CapabilityMarkers =
    [
        "what can you do",
        "what are your features",
        "show features",
        "що ти вмієш",
        "що ти можеш",
        "які дії",
        "які дії ти можеш",
        "чим ти можеш допомогти",
        "повний перелік",
        "чим ти корисна",
        "які твої можливості",
        "які твої функції"
    ];

    private static readonly string[] StatsMarkers =
    [
        "statistics",
        "show stats",
        "show statistics",
        "статистика",
        "покажи статистику"
    ];

    private static readonly string[] ImportPhotoMarkers =
    [
        "скину тобі фото",
        "скину фото",
        "перевір чи є нові слова",
        "send you a photo",
        "check if there are new words",
        "check new words from photo"
    ];

    private static readonly string[] VocabularyAddFlowMarkers =
    [
        "додай слово",
        "додати слово",
        "додай нове слово",
        "додай у словник",
        "додай в словник",
        "add word",
        "add new word",
        "add to dictionary",
        "add word to dictionary"
    ];

    private static readonly string[] SettingsOpenMarkers =
    [
        "налаштування",
        "settings",
        "show settings",
        "open settings"
    ];

    private static readonly string[] VocabularyOpenMarkers =
    [
        "словник",
        "vocabulary",
        "dictionary menu",
        "open dictionary",
        "open vocabulary"
    ];

    private static readonly string[] BatchModeMarkers =
    [
        "batch mode",
        "batch",
        "пакетний режим",
        "батч режим"
    ];

    private static readonly string[] ImportStartMarkers =
    [
        "імпорт",
        "import",
        "import words",
        "імпортуй",
        "з посилання",
        "from url"
    ];

    private static readonly string[] ImportFileMarkers =
    [
        "import from file",
        "імпорт з файлу",
        "з файлу",
        "upload file"
    ];

    private static readonly string[] ImportUrlMarkers =
    [
        "import from url",
        "імпорт з посилання",
        "з url",
        "за посиланням",
        "from link"
    ];

    private static readonly string[] ImportTextMarkers =
    [
        "import from text",
        "імпорт з тексту",
        "з тексту",
        "paste text to import"
    ];

    private static readonly string[] SaveModePanelMarkers =
    [
        "show save mode",
        "open save mode",
        "покажи режим збереження",
        "відкрий режим збереження"
    ];

    private static readonly string[] LanguagePanelMarkers =
    [
        "show language options",
        "open language settings",
        "change language",
        "обери мову",
        "покажи мови"
    ];

    private static readonly string[] OneDriveMarkers =
    [
        "onedrive",
        "graph"
    ];

    private static readonly string[] OneDriveStatusMarkers =
    [
        "статус onedrive",
        "статус graph",
        "onedrive status",
        "graph status",
        "перевір onedrive",
        "перевір graph"
    ];

    private static readonly string[] OneDriveLoginMarkers =
    [
        "увійди в onedrive",
        "увійти в onedrive",
        "підключи onedrive",
        "login onedrive",
        "log in onedrive",
        "connect onedrive",
        "graph login"
    ];

    private static readonly string[] OneDriveLogoutMarkers =
    [
        "вийди з onedrive",
        "вийти з onedrive",
        "logout onedrive",
        "log out onedrive",
        "disconnect onedrive",
        "graph logout"
    ];

    private static readonly string[] OneDriveSyncMarkers =
    [
        "синхронізуй",
        "синхронізація",
        "sync onedrive",
        "sync queue",
        "run sync"
    ];

    private static readonly string[] OneDriveRebuildMarkers =
    [
        "перезбери кеш",
        "перезібрати кеш",
        "rebuild cache",
        "rebuild index"
    ];

    private static readonly string[] OneDriveClearCacheMarkers =
    [
        "очистити кеш",
        "clear cache",
        "clear vocabulary cache"
    ];

    private static readonly string[] ShoppingOpenMarkers =
    [
        "покупки",
        "shopping",
        "shopping list"
    ];

    private static readonly string[] WeeklyOpenMarkers =
    [
        "тижневе меню",
        "weekly menu",
        "meal plan"
    ];

    private static readonly string[] NotionOpenMarkers =
    [
        "notion",
        "відкрий notion"
    ];

    private readonly IAiChatClient _aiChatClient;
    private readonly IUserMemoryRepository _userMemoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVocabularySaveModePreferenceService _saveModePreferenceService;
    private readonly IVocabularyStoragePreferenceService _storagePreferenceService;
    private readonly IVocabularyCardRepository _vocabularyCardRepository;
    private readonly IUserLocaleStateService? _userLocaleStateService;

    public AssistantConversationAgent(
        IAiChatClient aiChatClient,
        IUserMemoryRepository userMemoryRepository,
        IUnitOfWork unitOfWork,
        IVocabularySaveModePreferenceService saveModePreferenceService,
        IVocabularyStoragePreferenceService storagePreferenceService,
        IVocabularyCardRepository vocabularyCardRepository,
        IUserLocaleStateService? userLocaleStateService = null)
    {
        _aiChatClient = aiChatClient;
        _userMemoryRepository = userMemoryRepository;
        _unitOfWork = unitOfWork;
        _saveModePreferenceService = saveModePreferenceService;
        _storagePreferenceService = storagePreferenceService;
        _vocabularyCardRepository = vocabularyCardRepository;
        _userLocaleStateService = userLocaleStateService;
    }

    public string Name => "assistant-agent";

    public int Order => 50;

    public ConversationAgentRole Role => ConversationAgentRole.Assistant;

    public bool SupportsSlashCommands => false;

    public bool SupportsBatchInputs => false;

    public bool CanHandle(ConversationAgentContext context)
        => context.Input.StartsWith(ConversationInputMarkers.Chat, StringComparison.Ordinal);

    public async Task<ConversationAgentResult> HandleAsync(ConversationAgentContext context, CancellationToken cancellationToken = default)
    {
        var input = StripChatMarker(context.Input);
        if (string.IsNullOrWhiteSpace(input))
        {
            return ConversationAgentResult.Empty(Name, "assistant.chat.empty", "Tell me what you need, and I'll help.");
        }

        var locale = await ResolveLocaleAsync(context.Scope, cancellationToken);

        if (TryParseMemoryPreference(input, out var memoryPreference))
        {
            var memoryReply = await ApplyMemoryPreferenceAsync(context.Scope, memoryPreference, locale, cancellationToken);
            return ConversationAgentResult.Empty(Name, "assistant.memory.updated", memoryReply);
        }

        if (TryParseLanguageSwitchCommand(input, out var targetLocale))
        {
            if (string.IsNullOrWhiteSpace(targetLocale))
            {
                return ConversationAgentResult.Empty(Name, "assistant.settings.language.invalid", BuildLanguagePrompt(locale));
            }

            var languageReply = await ApplyLanguageChangeAsync(context.Scope, targetLocale, locale, cancellationToken);
            return ConversationAgentResult.Empty(Name, "assistant.settings.language.updated", languageReply);
        }

        if (TryParseSaveModeCommand(input, out var requestedMode))
        {
            if (!requestedMode.HasValue)
            {
                return ConversationAgentResult.Empty(Name, "assistant.settings.save_mode.invalid", BuildSaveModePrompt(locale));
            }

            var saveModeReply = await ApplySaveModeAsync(context.Scope, requestedMode.Value, locale, cancellationToken);
            return ConversationAgentResult.Empty(Name, "assistant.settings.save_mode.updated", saveModeReply);
        }

        if (IsVocabularyAddFlowRequest(input))
        {
            return ConversationAgentResult.Empty(
                Name,
                "assistant.vocabulary.add.start",
                BuildVocabularyAddFlowPrompt(locale));
        }

        if (TryParsePartOfSpeechCountRequest(input, out var marker))
        {
            var posCount = await BuildPartOfSpeechCountMessageAsync(marker, locale, cancellationToken);
            return ConversationAgentResult.Empty(Name, "assistant.vocabulary.stats.part_of_speech", posCount);
        }

        if (IsStatsRequest(input))
        {
            var stats = await BuildStatsMessageAsync(locale, cancellationToken);
            return ConversationAgentResult.Empty(Name, "assistant.vocabulary.stats", stats);
        }

        if (ContainsAny(input, CapabilityMarkers))
        {
            return ConversationAgentResult.Empty(Name, "assistant.capabilities", BuildCapabilitiesMessage(locale));
        }

        if (TryResolveChatActionIntent(input, locale, out var actionIntent, out var actionMessage))
        {
            return ConversationAgentResult.Empty(Name, actionIntent, actionMessage);
        }

        var fallback = await BuildChatReplyAsync(input, context.Scope, locale, cancellationToken);
        return ConversationAgentResult.Empty(Name, "assistant.chat", fallback);
    }

    private async Task<string> ApplySaveModeAsync(
        ConversationScope scope,
        VocabularySaveMode mode,
        string locale,
        CancellationToken cancellationToken)
    {
        await _saveModePreferenceService.SetModeAsync(scope, mode, cancellationToken);

        var modeText = _saveModePreferenceService.ToText(mode);
        var proofMode = await GetSettingsProofModeAsync(scope, cancellationToken);
        if (string.Equals(proofMode, ProofModeBrief, StringComparison.OrdinalIgnoreCase))
        {
            return BuildSaveModeUpdatedShortMessage(locale, modeText);
        }

        var storageMode = await _storagePreferenceService.GetModeAsync(scope, cancellationToken);
        return BuildSaveModeUpdatedProofMessage(locale, modeText, storageMode.ToString().ToLowerInvariant());
    }

    private async Task<string> ApplyLanguageChangeAsync(
        ConversationScope scope,
        string targetLocale,
        string currentLocale,
        CancellationToken cancellationToken)
    {
        if (_userLocaleStateService is null)
        {
            return currentLocale == LocalizationConstants.UkrainianLocale
                ? "Не вдалося змінити мову в цьому режимі."
                : "Language cannot be changed in this mode.";
        }

        var normalizedLocale = LocalizationConstants.NormalizeLocaleCode(targetLocale);
        var updatedLocale = await _userLocaleStateService.SetLocaleAsync(
            scope.Channel,
            scope.UserId,
            normalizedLocale,
            selectedManually: true,
            cancellationToken);

        var responseLocale = LocalizationConstants.NormalizeLocaleCode(updatedLocale);
        var targetDisplay = GetLanguageDisplayName(responseLocale, responseLocale);
        var proofMode = await GetSettingsProofModeAsync(scope, cancellationToken);

        if (string.Equals(proofMode, ProofModeBrief, StringComparison.OrdinalIgnoreCase))
        {
            return responseLocale == LocalizationConstants.UkrainianLocale
                ? $"Готово. Мову змінено на {targetDisplay}."
                : $"Done. Language changed to {targetDisplay}.";
        }

        return responseLocale == LocalizationConstants.UkrainianLocale
            ? string.Join(
                Environment.NewLine,
                [
                    $"✅ Готово. Мову змінено на {targetDisplay}.",
                    "",
                    "Поточні налаштування:",
                    $"• Мова: {targetDisplay}"
                ])
            : string.Join(
                Environment.NewLine,
                [
                    $"✅ Done. Language changed to {targetDisplay}.",
                    "",
                    "Current settings:",
                    $"• Language: {targetDisplay}"
                ]);
    }

    private async Task<string> BuildStatsMessageAsync(string locale, CancellationToken cancellationToken)
    {
        var total = await _vocabularyCardRepository.CountAllAsync(cancellationToken);
        if (total <= 0)
        {
            return locale == LocalizationConstants.UkrainianLocale
                ? "Словник порожній. Додай слова, і я покажу статистику."
                : "Your vocabulary list is empty. Add words and I will show detailed statistics.";
        }

        var deckStats = await _vocabularyCardRepository.GetDeckStatsAsync(cancellationToken);
        var posStats = await _vocabularyCardRepository.GetPartOfSpeechStatsAsync(cancellationToken);

        var builder = new StringBuilder();
        if (locale == LocalizationConstants.UkrainianLocale)
        {
            builder.AppendLine("📊 Статистика словника");
            builder.AppendLine();
            builder.AppendLine($"Усього слів: {total}");
            builder.AppendLine($"Файлів: {deckStats.Count} | Маркерів: {posStats.Count}");
            builder.AppendLine();
            builder.AppendLine("За частинами мови:");
            foreach (var part in posStats.OrderByDescending(x => x.Count).Take(10))
            {
                builder.AppendLine($"• {NormalizePartOfSpeechLabel(part.Marker ?? string.Empty, locale)}: {part.Count}");
            }

            var otherPartsCount = posStats.Count - 10;
            if (otherPartsCount > 0)
            {
                builder.AppendLine($"… і ще {otherPartsCount} маркер(ів).");
            }

            builder.AppendLine();
            builder.AppendLine("Топ файлів:");
            foreach (var deck in deckStats.OrderByDescending(x => x.Count).Take(10).Select((value, index) => (value, index)))
            {
                builder.AppendLine($"{deck.index + 1}) {deck.value.DeckFileName}: {deck.value.Count}");
            }
        }
        else
        {
            builder.AppendLine("📊 Vocabulary statistics");
            builder.AppendLine();
            builder.AppendLine($"Total words: {total}");
            builder.AppendLine($"Decks: {deckStats.Count} | POS markers: {posStats.Count}");
            builder.AppendLine();
            builder.AppendLine("By part of speech:");
            foreach (var part in posStats.OrderByDescending(x => x.Count).Take(10))
            {
                builder.AppendLine($"• {NormalizePartOfSpeechLabel(part.Marker ?? string.Empty, locale)}: {part.Count}");
            }

            var otherPartsCount = posStats.Count - 10;
            if (otherPartsCount > 0)
            {
                builder.AppendLine($"… and {otherPartsCount} more marker(s).");
            }

            builder.AppendLine();
            builder.AppendLine("Top decks:");
            foreach (var deck in deckStats.OrderByDescending(x => x.Count).Take(10).Select((value, index) => (value, index)))
            {
                builder.AppendLine($"{deck.index + 1}) {deck.value.DeckFileName}: {deck.value.Count}");
            }
        }

        return builder.ToString().Trim();
    }

    private async Task<string> BuildPartOfSpeechCountMessageAsync(
        string marker,
        string locale,
        CancellationToken cancellationToken)
    {
        var stats = await _vocabularyCardRepository.GetPartOfSpeechStatsAsync(cancellationToken);
        var count = stats
            .FirstOrDefault(x => string.Equals((x.Marker ?? string.Empty).Trim(), marker, StringComparison.OrdinalIgnoreCase))
            ?.Count ?? 0;

        var label = NormalizePartOfSpeechLabel(marker, locale);
        return locale == LocalizationConstants.UkrainianLocale
            ? $"У словнику зараз {count} ({label})."
            : $"Your dictionary currently has {count} {label}.";
    }

    private async Task<string> BuildChatReplyAsync(
        string input,
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        try
        {
            var note = await VocabularyScopedPreferenceMemory.GetScopedOrLegacyEntryAsync(
                _userMemoryRepository,
                UserPreferenceMemoryKeys.AssistantGeneralNote,
                scope,
                cancellationToken);

            var systemPrompt = BuildAssistantSystemPrompt(locale, note?.Value);
            var messages = new List<ConversationMessage>
            {
                ConversationMessage.Create(MessageRole.System, systemPrompt, DateTimeOffset.UtcNow),
                ConversationMessage.Create(MessageRole.User, input, DateTimeOffset.UtcNow)
            };

            var completion = await _aiChatClient.CompleteAsync(messages, cancellationToken);
            if (string.IsNullOrWhiteSpace(completion.Content))
            {
                return locale == LocalizationConstants.UkrainianLocale
                    ? "Я готова допомогти. Сформулюй задачу трохи детальніше."
                    : "I'm ready to help. Please describe your request in a bit more detail.";
            }

            return completion.Content.Trim();
        }
        catch
        {
            return locale == LocalizationConstants.UkrainianLocale
                ? "Зараз не вдалося згенерувати відповідь. Спробуй ще раз."
                : "I couldn't generate a reply right now. Please try again.";
        }
    }

    private async Task<string> ResolveLocaleAsync(ConversationScope scope, CancellationToken cancellationToken)
    {
        var localeEntry = await _userMemoryRepository.GetByKeyAsync(
            LocalizationConstants.LocaleMemoryKey,
            scope.Channel,
            scope.UserId,
            cancellationToken);

        return LocalizationConstants.NormalizeLocaleCode(localeEntry?.Value);
    }

    private async Task<string> GetSettingsProofModeAsync(ConversationScope scope, CancellationToken cancellationToken)
    {
        var entry = await VocabularyScopedPreferenceMemory.GetScopedOrLegacyEntryAsync(
            _userMemoryRepository,
            UserPreferenceMemoryKeys.AssistantSettingsProofMode,
            scope,
            cancellationToken);

        if (entry is null || string.IsNullOrWhiteSpace(entry.Value))
        {
            return ProofModeFull;
        }

        return string.Equals(entry.Value, ProofModeBrief, StringComparison.OrdinalIgnoreCase)
            ? ProofModeBrief
            : ProofModeFull;
    }

    private async Task<string> ApplyMemoryPreferenceAsync(
        ConversationScope scope,
        MemoryPreference preference,
        string locale,
        CancellationToken cancellationToken)
    {
        if (preference.Type == MemoryPreferenceType.SettingsProofMode)
        {
            await VocabularyScopedPreferenceMemory.UpsertScopedEntryAsync(
                _userMemoryRepository,
                _unitOfWork,
                UserPreferenceMemoryKeys.AssistantSettingsProofMode,
                preference.Value,
                scope,
                DateTimeOffset.UtcNow,
                cancellationToken);

            return string.Equals(preference.Value, ProofModeBrief, StringComparison.OrdinalIgnoreCase)
                ? (locale == LocalizationConstants.UkrainianLocale
                    ? "Добре, запам'ятала. Наступного разу після зміни налаштувань дам коротке підтвердження без повного блоку налаштувань."
                    : "Got it. Next time after settings changes I will send only a short confirmation without the full settings block.")
                : (locale == LocalizationConstants.UkrainianLocale
                    ? "Добре, запам'ятала. Після зміни налаштувань знову показуватиму повний підтверджувальний блок."
                    : "Got it. After settings changes I'll show the full confirmation block again.");
        }

        await VocabularyScopedPreferenceMemory.UpsertScopedEntryAsync(
            _userMemoryRepository,
            _unitOfWork,
            UserPreferenceMemoryKeys.AssistantGeneralNote,
            preference.Value,
            scope,
            DateTimeOffset.UtcNow,
            cancellationToken);

        return locale == LocalizationConstants.UkrainianLocale
            ? "Добре, запам'ятала."
            : "Got it. I saved this preference.";
    }

    private static bool TryResolveChatActionIntent(
        string input,
        string locale,
        out string intent,
        out string message)
    {
        intent = string.Empty;
        message = string.Empty;

        if (ContainsAny(input, OneDriveLoginMarkers))
        {
            intent = "assistant.onedrive.login";
            message = BuildActionMessage(locale, "Починаю вхід в OneDrive.", "Starting OneDrive sign-in.");
            return true;
        }

        if (ContainsAny(input, OneDriveLogoutMarkers))
        {
            intent = "assistant.onedrive.logout";
            message = BuildActionMessage(locale, "Виконую вихід з OneDrive.", "Signing out from OneDrive.");
            return true;
        }

        if (ContainsAny(input, OneDriveSyncMarkers))
        {
            intent = "assistant.onedrive.sync";
            message = BuildActionMessage(locale, "Запускаю синхронізацію черги.", "Running sync queue now.");
            return true;
        }

        if (ContainsAny(input, OneDriveRebuildMarkers))
        {
            intent = "assistant.onedrive.index.rebuild";
            message = BuildActionMessage(locale, "Готую перезбір кешу/індексу.", "Preparing cache/index rebuild.");
            return true;
        }

        if (ContainsAny(input, OneDriveClearCacheMarkers))
        {
            intent = "assistant.onedrive.cache.clear";
            message = BuildActionMessage(locale, "Готую очищення кешу.", "Preparing cache clear.");
            return true;
        }

        if (ContainsAny(input, OneDriveStatusMarkers))
        {
            intent = "assistant.onedrive.status";
            message = BuildActionMessage(locale, "Перевіряю статус OneDrive / Graph.", "Checking OneDrive / Graph status.");
            return true;
        }

        if (ContainsAny(input, ImportPhotoMarkers))
        {
            intent = "assistant.vocabulary.import.source.photo";
            message = BuildActionMessage(locale, "Готово, чекаю фото для імпорту.", "Ready. Send a photo for import.");
            return true;
        }

        if (ContainsAny(input, ImportFileMarkers))
        {
            intent = "assistant.vocabulary.import.source.file";
            message = BuildActionMessage(locale, "Готово, чекаю файл для імпорту.", "Ready. Send a file for import.");
            return true;
        }

        if (ContainsAny(input, ImportUrlMarkers))
        {
            intent = "assistant.vocabulary.import.source.url";
            message = BuildActionMessage(locale, "Готово, чекаю посилання для імпорту.", "Ready. Send a URL for import.");
            return true;
        }

        if (ContainsAny(input, ImportTextMarkers))
        {
            intent = "assistant.vocabulary.import.source.text";
            message = BuildActionMessage(locale, "Готово, чекаю текст для імпорту.", "Ready. Send text for import.");
            return true;
        }

        if (ContainsAny(input, BatchModeMarkers))
        {
            intent = "assistant.vocabulary.batch.start";
            message = BuildActionMessage(locale, "Відкриваю batch-режим.", "Opening batch mode.");
            return true;
        }

        if (ContainsAny(input, ImportStartMarkers))
        {
            intent = "assistant.vocabulary.import.start";
            message = BuildActionMessage(locale, "Відкриваю імпорт слів.", "Opening vocabulary import.");
            return true;
        }

        if (ContainsAny(input, SaveModePanelMarkers))
        {
            intent = "assistant.settings.save_mode.open";
            message = BuildActionMessage(locale, "Відкриваю налаштування режиму збереження.", "Opening save mode settings.");
            return true;
        }

        if (ContainsAny(input, LanguagePanelMarkers))
        {
            intent = "assistant.settings.language.open";
            message = BuildActionMessage(locale, "Відкриваю вибір мови.", "Opening language options.");
            return true;
        }

        if (ContainsAny(input, SettingsOpenMarkers))
        {
            intent = "assistant.settings.open";
            message = BuildActionMessage(locale, "Відкриваю налаштування.", "Opening settings.");
            return true;
        }

        if (ContainsAny(input, VocabularyOpenMarkers))
        {
            intent = "assistant.vocabulary.open";
            message = BuildActionMessage(locale, "Відкриваю розділ словника.", "Opening vocabulary section.");
            return true;
        }

        if (ContainsAny(input, OneDriveMarkers))
        {
            intent = "assistant.onedrive.open";
            message = BuildActionMessage(locale, "Відкриваю OneDrive / Graph.", "Opening OneDrive / Graph.");
            return true;
        }

        if (ContainsAny(input, ShoppingOpenMarkers))
        {
            intent = "assistant.shopping.open";
            message = BuildActionMessage(locale, "Відкриваю покупки.", "Opening shopping section.");
            return true;
        }

        if (ContainsAny(input, WeeklyOpenMarkers))
        {
            intent = "assistant.weekly.open";
            message = BuildActionMessage(locale, "Відкриваю меню.", "Opening weekly menu section.");
            return true;
        }

        if (ContainsAny(input, NotionOpenMarkers))
        {
            intent = "assistant.settings.notion.open";
            message = BuildActionMessage(locale, "Відкриваю Notion (скоро).", "Opening Notion (coming soon).");
            return true;
        }

        return false;
    }

    private static bool TryParseSaveModeCommand(string input, out VocabularySaveMode? mode)
    {
        mode = null;
        if (!ContainsAny(input, SaveModeTopicMarkers))
        {
            return false;
        }

        var normalized = Normalize(input);
        if (normalized.Contains("auto", StringComparison.Ordinal)
            || normalized.Contains("авто", StringComparison.Ordinal)
            || normalized.Contains("автомат", StringComparison.Ordinal))
        {
            mode = VocabularySaveMode.Auto;
            return true;
        }

        if (normalized.Contains("ask", StringComparison.Ordinal)
            || normalized.Contains("питай", StringComparison.Ordinal)
            || normalized.Contains("запит", StringComparison.Ordinal))
        {
            mode = VocabularySaveMode.Ask;
            return true;
        }

        if (normalized.Contains("off", StringComparison.Ordinal)
            || normalized.Contains("вимк", StringComparison.Ordinal)
            || normalized.Contains("disable", StringComparison.Ordinal))
        {
            mode = VocabularySaveMode.Off;
            return true;
        }

        return true;
    }

    private static bool TryParseLanguageSwitchCommand(string input, out string? locale)
    {
        locale = null;
        var normalized = Normalize(input);

        var isLanguageTopic = normalized.Contains("мов", StringComparison.Ordinal)
            || normalized.Contains("language", StringComparison.Ordinal);
        var isSwitchAction = normalized.Contains("переключ", StringComparison.Ordinal)
            || normalized.Contains("змін", StringComparison.Ordinal)
            || normalized.Contains("switch", StringComparison.Ordinal)
            || normalized.Contains("change", StringComparison.Ordinal)
            || normalized.Contains("set ", StringComparison.Ordinal);

        if (!isLanguageTopic || !isSwitchAction)
        {
            return false;
        }

        if (normalized.Contains("укра", StringComparison.Ordinal) || normalized.Contains("ukrain", StringComparison.Ordinal))
        {
            locale = LocalizationConstants.UkrainianLocale;
            return true;
        }

        if (normalized.Contains("англ", StringComparison.Ordinal) || normalized.Contains("english", StringComparison.Ordinal))
        {
            locale = LocalizationConstants.EnglishLocale;
            return true;
        }

        return true;
    }

    private static bool TryParsePartOfSpeechCountRequest(string input, out string marker)
    {
        marker = string.Empty;
        var normalized = Normalize(input);

        var asksCount = normalized.Contains("скільки", StringComparison.Ordinal)
            || normalized.Contains("how many", StringComparison.Ordinal)
            || normalized.Contains("count", StringComparison.Ordinal);

        if (!asksCount)
        {
            return false;
        }

        marker = normalized switch
        {
            var s when s.Contains("дієслів", StringComparison.Ordinal) || s.Contains("дієсл", StringComparison.Ordinal) || s.Contains("verb", StringComparison.Ordinal) => "v",
            var s when s.Contains("іменник", StringComparison.Ordinal) || s.Contains("noun", StringComparison.Ordinal) => "n",
            var s when s.Contains("прикмет", StringComparison.Ordinal) || s.Contains("adjective", StringComparison.Ordinal) => "adj",
            var s when s.Contains("прислів", StringComparison.Ordinal) || s.Contains("adverb", StringComparison.Ordinal) => "adv",
            var s when s.Contains("приймен", StringComparison.Ordinal) || s.Contains("preposition", StringComparison.Ordinal) => "prep",
            var s when s.Contains("фразов", StringComparison.Ordinal) || s.Contains("phrasal", StringComparison.Ordinal) => "pv",
            var s when s.Contains("неправильн", StringComparison.Ordinal) || s.Contains("irregular", StringComparison.Ordinal) => "iv",
            var s when s.Contains("стал", StringComparison.Ordinal) || s.Contains("вираз", StringComparison.Ordinal) || s.Contains("persistent expression", StringComparison.Ordinal) => "pe",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(marker);
    }

    private static bool TryParseMemoryPreference(string input, out MemoryPreference preference)
    {
        preference = MemoryPreference.None;

        var normalized = Normalize(input);
        var isMemoryCommand = normalized.StartsWith("запам", StringComparison.Ordinal)
            || normalized.StartsWith("remember", StringComparison.Ordinal)
            || normalized.Contains("запам", StringComparison.Ordinal)
            || normalized.Contains("remember", StringComparison.Ordinal);

        if (!isMemoryCommand)
        {
            return false;
        }

        if ((normalized.Contains("не треба", StringComparison.Ordinal) || normalized.Contains("don't", StringComparison.Ordinal))
            && normalized.Contains("налашт", StringComparison.Ordinal))
        {
            preference = new MemoryPreference(MemoryPreferenceType.SettingsProofMode, ProofModeBrief);
            return true;
        }

        if ((normalized.Contains("показуй", StringComparison.Ordinal) || normalized.Contains("show", StringComparison.Ordinal))
            && normalized.Contains("налашт", StringComparison.Ordinal))
        {
            preference = new MemoryPreference(MemoryPreferenceType.SettingsProofMode, ProofModeFull);
            return true;
        }

        preference = new MemoryPreference(MemoryPreferenceType.GeneralNote, input.Trim());
        return true;
    }

    private static string BuildAssistantSystemPrompt(string locale, string? note)
    {
        var languageName = locale switch
        {
            LocalizationConstants.UkrainianLocale => "Ukrainian",            _ => "English"
        };

        var builder = new StringBuilder();
        builder.AppendLine("You are Lagertha, a practical AI assistant inside an app.");
        builder.AppendLine($"Always reply in {languageName}.");
        builder.AppendLine("Be concise and actionable.");
        builder.AppendLine("Never invent actions that were not executed.");
        builder.AppendLine("Current app capabilities:");
        builder.AppendLine("- vocabulary: single word lookup, batch mode, import from photo/file/url/text, dictionary statistics");
        builder.AppendLine("- settings: language, save mode (ask/auto/off), storage mode, OneDrive/Graph");
        builder.AppendLine("- one-click workflows for Telegram buttons");
        builder.AppendLine("If user asks for unavailable feature, clearly say it's planned.");
        if (!string.IsNullOrWhiteSpace(note))
        {
            builder.AppendLine();
            builder.AppendLine("User preference note:");
            builder.AppendLine(note.Trim());
        }

        return builder.ToString().Trim();
    }

    private static string BuildCapabilitiesMessage(string locale)
    {
        if (locale == LocalizationConstants.UkrainianLocale)
        {
            return string.Join(
                Environment.NewLine,
                [
                    "Я можу допомогти з таким:",
                    "• Додавати слова в словник (одне слово або batch).",
                    "• Імпортувати слова з фото, файлу, посилання або тексту.",
                    "• Показувати статистику словника.",
                    "• Змінювати налаштування (мова, режим збереження ask/auto/off, режим сховища).",
                    "• Працювати з OneDrive/Graph: статус, вхід, синхронізація, кеш.",
                    "• Запам'ятовувати твої преференції для наступних відповідей."
                ]);
        }

        return string.Join(
            Environment.NewLine,
            [
                "Here is what I can do:",
                "• Add words to your vocabulary (single or batch mode).",
                "• Import words from photo, file, URL, or plain text.",
                "• Show vocabulary statistics.",
                "• Change settings (language, save mode ask/auto/off, storage mode).",
                "• Work with OneDrive/Graph: status, login, sync, cache actions.",
                "• Remember your response preferences for future actions."
            ]);
    }

    private static string BuildSaveModePrompt(string locale)
    {
        return locale == LocalizationConstants.UkrainianLocale
            ? "Уточни, будь ласка, режим збереження: auto, ask або off."
            : "Please specify the save mode: auto, ask, or off.";
    }

        private static string BuildLanguagePrompt(string locale)
    {
        return locale == LocalizationConstants.UkrainianLocale
            ? "??????, ???? ?????, ????: ?????????? ??? english."
            : "Please specify the target language: Ukrainian or English.";
    }
    private static string BuildVocabularyAddFlowPrompt(string locale)
    {
        return locale == LocalizationConstants.UkrainianLocale
            ? "Будь ласка, надішліть слово, яке потрібно додати у словник."
            : "Please send the word you want to add to the dictionary.";
    }

    private static string BuildActionMessage(string locale, string ukrainianText, string englishText)
        => locale == LocalizationConstants.UkrainianLocale
            ? ukrainianText
            : englishText;

    private static string BuildSaveModeUpdatedShortMessage(string locale, string modeText)
    {
        return locale == LocalizationConstants.UkrainianLocale
            ? $"Готово. Режим збереження змінено на {modeText}."
            : $"Done. Save mode has been changed to {modeText}.";
    }

    private static string BuildSaveModeUpdatedProofMessage(string locale, string modeText, string storageModeText)
    {
        if (locale == LocalizationConstants.UkrainianLocale)
        {
            return string.Join(
                Environment.NewLine,
                [
                    $"✅ Готово. Режим збереження змінено на {modeText}.",
                    "",
                    "Поточні налаштування:",
                    $"• Режим збереження: {modeText}",
                    $"• Режим сховища: {storageModeText}"
                ]);
        }

        return string.Join(
            Environment.NewLine,
            [
                $"✅ Done. Save mode changed to {modeText}.",
                "",
                "Current settings:",
                $"• Save mode: {modeText}",
                $"• Storage mode: {storageModeText}"
            ]);
    }

    private static bool ContainsAny(string value, IEnumerable<string> markers)
    {
        var normalized = Normalize(value);
        return markers.Any(marker => normalized.Contains(Normalize(marker), StringComparison.Ordinal));
    }

    private static bool IsStatsRequest(string input)
    {
        var normalized = Normalize(input);
        return ContainsAny(normalized, StatsMarkers)
               || normalized.Contains("статистик", StringComparison.Ordinal)
               || normalized.Contains("statistics", StringComparison.Ordinal)
               || normalized.Contains("stats", StringComparison.Ordinal);
    }

    private static bool IsVocabularyAddFlowRequest(string input)
    {
        var normalized = Normalize(input);
        if (ContainsAny(normalized, VocabularyAddFlowMarkers))
        {
            return true;
        }

        var mentionsAddAction = normalized.Contains("add", StringComparison.Ordinal)
                                || normalized.Contains("додай", StringComparison.Ordinal)
                                || normalized.Contains("додати", StringComparison.Ordinal);
        if (!mentionsAddAction)
        {
            return false;
        }

        return normalized.Contains("word", StringComparison.Ordinal)
               || normalized.Contains("слово", StringComparison.Ordinal)
               || normalized.Contains("dictionary", StringComparison.Ordinal)
               || normalized.Contains("словник", StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static string StripChatMarker(string input)
    {
        if (!input.StartsWith(ConversationInputMarkers.Chat, StringComparison.Ordinal))
        {
            return input;
        }

        return input[ConversationInputMarkers.Chat.Length..].Trim();
    }

    private static string NormalizePartOfSpeechLabel(string marker, string locale)
    {
        var normalized = marker?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return locale == LocalizationConstants.UkrainianLocale ? "(без маркера)" : "(unclassified)";
        }

        return normalized switch
        {
            "n" => locale == LocalizationConstants.UkrainianLocale ? "іменники" : "nouns",
            "v" => locale == LocalizationConstants.UkrainianLocale ? "дієслова" : "verbs",
            "adj" => locale == LocalizationConstants.UkrainianLocale ? "прикметники" : "adjectives",
            "adv" => locale == LocalizationConstants.UkrainianLocale ? "прислівники" : "adverbs",
            "prep" => locale == LocalizationConstants.UkrainianLocale ? "прийменники" : "prepositions",
            "pv" => locale == LocalizationConstants.UkrainianLocale ? "фразові дієслова" : "phrasal verbs",
            "iv" => locale == LocalizationConstants.UkrainianLocale ? "неправильні дієслова" : "irregular verbs",
            "pe" => locale == LocalizationConstants.UkrainianLocale ? "сталі вирази" : "persistent expressions",
            _ => normalized
        };
    }

    private static string GetLanguageDisplayName(string locale, string responseLocale)
    {
        var normalized = LocalizationConstants.NormalizeLocaleCode(locale);
        var displayInUkrainian = normalized switch
        {
            LocalizationConstants.UkrainianLocale => "українську",
            LocalizationConstants.EnglishLocale => "english",            _ => normalized
        };

        if (responseLocale == LocalizationConstants.UkrainianLocale)
        {
            return displayInUkrainian;
        }

        return normalized switch
        {
            LocalizationConstants.UkrainianLocale => "Ukrainian",
            LocalizationConstants.EnglishLocale => "English",            _ => normalized
        };
    }

    private enum MemoryPreferenceType
    {
        None = 0,
        SettingsProofMode = 1,
        GeneralNote = 2
    }

    private sealed record MemoryPreference(MemoryPreferenceType Type, string Value)
    {
        public static MemoryPreference None { get; } = new(MemoryPreferenceType.None, string.Empty);
    }
}




