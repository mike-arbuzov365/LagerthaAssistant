namespace LagerthaAssistant.Infrastructure;

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Domain.Abstractions;
using LagerthaAssistant.Infrastructure.AI;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.Infrastructure.Options;
using LagerthaAssistant.Infrastructure.Repositories;
using LagerthaAssistant.Infrastructure.Services.Vocabulary;
using LagerthaAssistant.Infrastructure.Time;

public static class DependencyInjection
{
    public static IServiceCollection AddLagerthaAssistant(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(OpenAiConstants.SectionName);

        var options = new OpenAiOptions
        {
            BaseUrl = section[OpenAiConstants.BaseUrlKey] ?? OpenAiConstants.DefaultBaseUrl,
            Model = section[OpenAiConstants.ModelKey] ?? OpenAiConstants.DefaultModel,
            ApiKey = section[OpenAiConstants.ApiKeyKey],
            Temperature = ParseDouble(section[OpenAiConstants.TemperatureKey], OpenAiConstants.DefaultTemperature)
        };

        var envApiKey = Environment.GetEnvironmentVariable(OpenAiConstants.ApiKeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envApiKey))
        {
            options.ApiKey = envApiKey;
        }

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

        services.AddDbContext<AppDbContext>(db => db.UseSqlServer(connectionString));

        services.AddSingleton(options);
        services.AddSingleton(vocabularyOptions);
        services.AddSingleton(storageOptions);
        services.AddSingleton(graphOptions);
        services.AddSingleton(notionOptions);
        services.AddSingleton<IClock, SystemClock>();

        services.AddSingleton(sp =>
        {
            var currentOptions = sp.GetRequiredService<OpenAiOptions>();
            return new HttpClient
            {
                BaseAddress = new Uri(currentOptions.BaseUrl),
                Timeout = TimeSpan.FromSeconds(OpenAiConstants.HttpTimeoutSeconds)
            };
        });

        services.AddScoped<IVocabularyStorageModeProvider, VocabularyStorageModeProvider>();
        services.AddSingleton<IGraphAuthService, GraphAuthService>();
        services.AddSingleton<IGraphDriveClient, GraphDriveClient>();

        services.AddScoped<IAiChatClient, OpenAiChatClient>();
        services.AddScoped<INotionCardExportService, NotionCardExportService>();
        services.AddScoped<IConversationSessionRepository, ConversationSessionRepository>();
        services.AddScoped<IConversationHistoryRepository, ConversationHistoryRepository>();
        services.AddScoped<IUserMemoryRepository, UserMemoryRepository>();
        services.AddScoped<ISystemPromptRepository, SystemPromptRepository>();
        services.AddScoped<ISystemPromptProposalRepository, SystemPromptProposalRepository>();
        services.AddScoped<IVocabularyCardRepository, VocabularyCardRepository>();
        services.AddScoped<IVocabularySyncJobRepository, VocabularySyncJobRepository>();
        services.AddScoped<IConversationIntentMetricRepository, ConversationIntentMetricRepository>();

        services.AddScoped<VocabularyDeckService>();
        services.AddScoped<GraphVocabularyDeckService>();
        services.AddScoped<IVocabularyDeckBackend>(sp => sp.GetRequiredService<VocabularyDeckService>());
        services.AddScoped<IVocabularyDeckBackend>(sp => sp.GetRequiredService<GraphVocabularyDeckService>());
        services.AddScoped<IVocabularyDeckBackendResolver, VocabularyDeckBackendResolver>();
        services.AddScoped<IVocabularyDeckService, SwitchableVocabularyDeckService>();
        services.AddScoped<IVocabularyDeckModeService, VocabularyDeckModeService>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

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
