namespace LagerthaAssistant.Infrastructure;

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Repositories.Food;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Options;
using LagerthaAssistant.Domain.Abstractions;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.Infrastructure.Options;
using LagerthaAssistant.Infrastructure.Repositories;
using LagerthaAssistant.Infrastructure.Services;
using LagerthaAssistant.Infrastructure.Services.Food;
using LagerthaAssistant.Infrastructure.Services.Vocabulary;
using LagerthaAssistant.Infrastructure.Time;
using SharedBotKernel.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection AddLagerthaAssistant(this IServiceCollection services, IConfiguration configuration)
    {
        // AI clients, options, IClock — handled by SharedBotKernel (#004, #007)
        services.AddKernelServices(configuration);

        var connectionString = configuration.GetConnectionString(PersistenceConstants.ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{PersistenceConstants.ConnectionStringName}' not found.");

        var vocabularySection = configuration.GetSection(VocabularyDeckConstants.SectionName);
        var vocabularyOptions = new VocabularyDeckOptions
        {
            FolderPath = vocabularySection[VocabularyDeckConstants.FolderPathKey] ?? "%OneDrive%\\Apps\\Flashcards Deluxe",
            FilePattern = vocabularySection[VocabularyDeckConstants.FilePatternKey] ?? "wm-*.xlsx",
            ReadOnlyFileNames = ReadStringArray(
                vocabularySection.GetSection(VocabularyDeckConstants.ReadOnlyFileNamesKey),
                VocabularyDeckConstants.DefaultReadOnlyFileNames),
            NounDeckFileName = vocabularySection[VocabularyDeckConstants.NounDeckFileNameKey] ?? "wm-nouns-ua-en.xlsx",
            VerbDeckFileName = vocabularySection[VocabularyDeckConstants.VerbDeckFileNameKey] ?? "wm-verbs-us-en.xlsx",
            IrregularVerbDeckFileName = vocabularySection[VocabularyDeckConstants.IrregularVerbDeckFileNameKey] ?? "wm-irregular-verbs-ua-en.xlsx",
            PhrasalVerbDeckFileName = vocabularySection[VocabularyDeckConstants.PhrasalVerbDeckFileNameKey] ?? "wm-phrasal-verbs-ua-en.xlsx",
            AdjectiveDeckFileName = vocabularySection[VocabularyDeckConstants.AdjectiveDeckFileNameKey] ?? "wm-adjectives-ua-en.xlsx",
            AdverbDeckFileName = vocabularySection[VocabularyDeckConstants.AdverbDeckFileNameKey] ?? "wm-adverbs-ua-en.xlsx",
            PrepositionDeckFileName = vocabularySection[VocabularyDeckConstants.PrepositionDeckFileNameKey] ?? "wm-prepositions-ua-en.xlsx",
            ConjunctionDeckFileName = vocabularySection[VocabularyDeckConstants.ConjunctionDeckFileNameKey] ?? "wm-conjunctions-ua-en.xlsx",
            PronounDeckFileName = vocabularySection[VocabularyDeckConstants.PronounDeckFileNameKey] ?? "wm-pronouns-ua-en.xlsx",
            PersistentExpressionDeckFileName = vocabularySection[VocabularyDeckConstants.PersistentExpressionDeckFileNameKey] ?? "wm-persistant-expressions-ua-en.xlsx",
            FallbackDeckFileName = vocabularySection[VocabularyDeckConstants.FallbackDeckFileNameKey] ?? "wm-vocabulary-1-grade-ua-en.xlsx"
        };

        var storageSection = configuration.GetSection(VocabularyStorageConstants.SectionName);
        var storageOptions = new VocabularyStorageOptions
        {
            DefaultMode = storageSection[VocabularyStorageConstants.DefaultModeKey] ?? "local"
        };

        var graphSection = configuration.GetSection(GraphConstants.SectionName);
        var graphOptions = new GraphOptions
        {
            TenantId = graphSection[GraphConstants.TenantIdKey] ?? "common",
            ClientId = graphSection[GraphConstants.ClientIdKey] ?? string.Empty,
            Scopes = ReadStringArray(graphSection.GetSection(GraphConstants.ScopesKey), ["User.Read", "Files.ReadWrite", "offline_access"]),
            RootPath = graphSection[GraphConstants.RootPathKey] ?? "/Apps/Flashcards Deluxe",
            TokenCachePath = graphSection[GraphConstants.TokenCachePathKey] ?? "%LOCALAPPDATA%\\LagerthaAssistant\\graph-token.json"
        };

        var notionSection = configuration.GetSection(NotionConstants.SectionName);
        var notionOptions = new NotionOptions
        {
            Enabled = ParseBool(notionSection[NotionConstants.EnabledKey], false),
            ApiKey = notionSection[NotionConstants.ApiKeyKey] ?? string.Empty,
            DatabaseId = notionSection[NotionConstants.DatabaseIdKey] ?? string.Empty,
            ApiBaseUrl = notionSection[NotionConstants.ApiBaseUrlKey] ?? "https://api.notion.com/v1",
            Version = notionSection[NotionConstants.VersionKey] ?? "2022-06-28",
            ConflictMode = notionSection[NotionConstants.ConflictModeKey] ?? "update",
            RequestTimeoutSeconds = ParseInt(notionSection[NotionConstants.RequestTimeoutSecondsKey], 60),
            KeyPropertyName = notionSection[NotionConstants.KeyPropertyNameKey] ?? "Key",
            WordPropertyName = notionSection[NotionConstants.WordPropertyNameKey] ?? "Word",
            MeaningPropertyName = notionSection[NotionConstants.MeaningPropertyNameKey] ?? "Meaning",
            ExamplesPropertyName = notionSection[NotionConstants.ExamplesPropertyNameKey] ?? "Examples",
            PartOfSpeechPropertyName = notionSection[NotionConstants.PartOfSpeechPropertyNameKey] ?? "PartOfSpeech",
            DeckPropertyName = notionSection[NotionConstants.DeckPropertyNameKey] ?? "DeckFile",
            StorageModePropertyName = notionSection[NotionConstants.StorageModePropertyNameKey] ?? "StorageMode",
            RowNumberPropertyName = notionSection[NotionConstants.RowNumberPropertyNameKey] ?? "RowNumber",
            LastSeenPropertyName = notionSection[NotionConstants.LastSeenPropertyNameKey] ?? "LastSeenAtUtc"
        };

        services.AddDbContext<AppDbContext>(db => db.UseNpgsql(connectionString));

        services.AddSingleton(vocabularyOptions);
        services.AddSingleton(storageOptions);
        services.AddSingleton(graphOptions);
        services.AddSingleton(notionOptions);
        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<IVocabularyStorageModeProvider, VocabularyStorageModeProvider>();
        services.AddSingleton<IGraphAuthService, GraphAuthService>();
        services.AddSingleton<IGraphDriveClient, GraphDriveClient>();

        // AI: low-level clients registered by AddKernelServices(); Lagertha-specific resolving layer:
        services.AddScoped<IAiCredentialRepository, AiCredentialRepository>();
        services.AddScoped<IAiRuntimeSettingsService, AiRuntimeSettingsService>();
        services.AddScoped<IAiChatClient, ResolvingAiChatClient>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddScoped<INotionCardExportService, NotionCardExportService>();
        services.AddScoped<IConversationSessionRepository, ConversationSessionRepository>();
        services.AddScoped<IConversationHistoryRepository, ConversationHistoryRepository>();
        services.AddScoped<IUserMemoryRepository, UserMemoryRepository>();
        services.AddScoped<ISystemPromptRepository, SystemPromptRepository>();
        services.AddScoped<IVocabularyCardRepository, VocabularyCardRepository>();
        services.AddScoped<IVocabularySyncJobRepository, VocabularySyncJobRepository>();
        services.AddScoped<IConversationIntentMetricRepository, ConversationIntentMetricRepository>();
        services.AddScoped<ITelegramProcessedUpdateRepository, TelegramProcessedUpdateRepository>();

        services.AddSingleton<IWordValidationService, WordValidationService>();
        services.AddScoped<VocabularyDeckService>();
        // Graph backend keeps a remote mirror cache; singleton lifetime avoids re-downloading
        // every writable deck on each API request.
        services.AddSingleton<GraphVocabularyDeckService>();
        services.AddScoped<IVocabularyDeckBackend>(sp => sp.GetRequiredService<VocabularyDeckService>());
        services.AddSingleton<IVocabularyDeckBackend>(sp => sp.GetRequiredService<GraphVocabularyDeckService>());
        services.AddScoped<IVocabularyDeckBackendResolver, VocabularyDeckBackendResolver>();
        services.AddScoped<IVocabularyDeckService, SwitchableVocabularyDeckService>();
        services.AddScoped<IVocabularyDeckModeService, VocabularyDeckModeService>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Food tracking repos + Notion client options ───────────────────────
        var notionFoodSection = configuration.GetSection("NotionFood");
        var notionFoodOptions = new NotionFoodOptions
        {
            Enabled = ParseBool(notionFoodSection["Enabled"], false),
            ApiKey = notionFoodSection["ApiKey"] ?? string.Empty,
            InventoryDatabaseId = notionFoodSection["InventoryDatabaseId"] ?? string.Empty,
            MealPlansDatabaseId = notionFoodSection["MealPlansDatabaseId"] ?? string.Empty,
            GroceryListDatabaseId = notionFoodSection["GroceryListDatabaseId"] ?? string.Empty,
            RequestTimeoutSeconds = ParseInt(notionFoodSection["RequestTimeoutSeconds"], 60)
        };
        services.AddSingleton(notionFoodOptions);

        var foodSyncSection = configuration.GetSection("FoodSync");
        var foodSyncOptions = new FoodSyncOptions
        {
            MaxSyncAttempts = ParseInt(foodSyncSection["MaxSyncAttempts"], 5),
            TombstoneRetentionDays = ParseInt(foodSyncSection["TombstoneRetentionDays"], 7)
        };
        services.AddSingleton(foodSyncOptions);

        services.AddScoped<IFoodItemRepository, FoodItemRepository>();
        services.AddScoped<IMealRepository, MealRepository>();
        services.AddScoped<IGroceryListRepository, GroceryListRepository>();
        services.AddScoped<IMealHistoryRepository, MealHistoryRepository>();

        return services;
    }

    private static string[] ReadStringArray(IConfigurationSection section, IReadOnlyList<string> fallback)
    {
        var values = section.GetChildren()
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToArray();

        return values.Length > 0
            ? values
            : fallback.ToArray();
    }

    private static double ParseDouble(string? value, double fallback)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static int ParseInt(string? value, int fallback)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static bool ParseBool(string? value, bool fallback)
    {
        return bool.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }
}
