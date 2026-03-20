namespace LagerthaAssistant.Application.Services.Agents;

using System.Text;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Agents;
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

    private readonly IAiChatClient _aiChatClient;
    private readonly IUserMemoryRepository _userMemoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVocabularySaveModePreferenceService _saveModePreferenceService;
    private readonly IVocabularyStoragePreferenceService _storagePreferenceService;
    private readonly IVocabularyCardRepository _vocabularyCardRepository;

    public AssistantConversationAgent(
        IAiChatClient aiChatClient,
        IUserMemoryRepository userMemoryRepository,
        IUnitOfWork unitOfWork,
        IVocabularySaveModePreferenceService saveModePreferenceService,
        IVocabularyStoragePreferenceService storagePreferenceService,
        IVocabularyCardRepository vocabularyCardRepository)
    {
        _aiChatClient = aiChatClient;
        _userMemoryRepository = userMemoryRepository;
        _unitOfWork = unitOfWork;
        _saveModePreferenceService = saveModePreferenceService;
        _storagePreferenceService = storagePreferenceService;
        _vocabularyCardRepository = vocabularyCardRepository;
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

        if (TryParseSaveModeCommand(input, out var requestedMode))
        {
            if (!requestedMode.HasValue)
            {
                return ConversationAgentResult.Empty(Name, "assistant.settings.save_mode.invalid", BuildSaveModePrompt(locale));
            }

            var saveModeReply = await ApplySaveModeAsync(context.Scope, requestedMode.Value, locale, cancellationToken);
            return ConversationAgentResult.Empty(Name, "assistant.settings.save_mode.updated", saveModeReply);
        }

        if (ContainsAny(input, StatsMarkers))
        {
            var stats = await BuildStatsMessageAsync(locale, cancellationToken);
            return ConversationAgentResult.Empty(Name, "assistant.vocabulary.stats", stats);
        }

        if (ContainsAny(input, CapabilityMarkers))
        {
            return ConversationAgentResult.Empty(Name, "assistant.capabilities", BuildCapabilitiesMessage(locale));
        }

        if (ContainsAny(input, ImportPhotoMarkers))
        {
            return ConversationAgentResult.Empty(Name, "assistant.import.photo.ready", BuildPhotoReadyMessage(locale));
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
            LocalizationConstants.UkrainianLocale => "Ukrainian",
            LocalizationConstants.SpanishLocale => "Spanish",
            LocalizationConstants.FrenchLocale => "French",
            LocalizationConstants.GermanLocale => "German",
            LocalizationConstants.PolishLocale => "Polish",
            _ => "English"
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

    private static string BuildPhotoReadyMessage(string locale)
    {
        return locale == LocalizationConstants.UkrainianLocale
            ? "Звісно. У мене є агент і потрібний функціонал. Скидай фото, і я перевірю нові слова для тебе."
            : "Sure. I have the required agent flow and can help with that. Send the photo and I'll check for new words.";
    }

    private static string BuildSaveModePrompt(string locale)
    {
        return locale == LocalizationConstants.UkrainianLocale
            ? "Уточни, будь ласка, режим збереження: auto, ask або off."
            : "Please specify the save mode: auto, ask, or off.";
    }

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
