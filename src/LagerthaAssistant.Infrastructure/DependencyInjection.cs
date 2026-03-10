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
            FallbackDeckFileName = vocabularySection[VocabularyDeckConstants.FallbackDeckFileNameKey] ?? "wm-vocabulary-1-grade-ua-en.xlsx"
        };

        services.AddDbContext<AppDbContext>(db => db.UseSqlServer(connectionString));

        services.AddSingleton(options);
        services.AddSingleton(vocabularyOptions);
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

        services.AddScoped<IAiChatClient, OpenAiChatClient>();
        services.AddScoped<IConversationSessionRepository, ConversationSessionRepository>();
        services.AddScoped<IConversationHistoryRepository, ConversationHistoryRepository>();
        services.AddScoped<IUserMemoryRepository, UserMemoryRepository>();
        services.AddScoped<ISystemPromptRepository, SystemPromptRepository>();
        services.AddScoped<ISystemPromptProposalRepository, SystemPromptProposalRepository>();
        services.AddScoped<IVocabularyDeckService, VocabularyDeckService>();
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
}
