using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.DependencyInjection;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;
using LagerthaAssistant.Domain.AI;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Infrastructure;
using LagerthaAssistant.Infrastructure.Options;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.UI.Constants;
using System.Globalization;
using System.Text;

namespace LagerthaAssistant.UI;

internal static partial class Program
{
    private const string SaveModeMemoryKey = "ui.save.mode";
    private const string StorageModeMemoryKey = "ui.storage.mode";
    private const string UiChannel = "ui";
    private const string UiConversationId = "main";
    private const string UiUserIdEnvironmentVariable = "LAGERTHA_USER_ID";
    private const string UiConversationIdEnvironmentVariable = "LAGERTHA_CONVERSATION_ID";

    private enum SaveMode
    {
        Ask,
        Auto,
        Off
    }

    private enum SaveConfirmationChoice
    {
        Yes,
        YesDontAskAgain,
        No,
        SaveToOtherDeck
    }

    private enum BatchSaveConfirmationChoice
    {
        SaveAll,
        SaveAllDontAskAgain,
        ReviewTargets,
        SkipAll
    }

    private enum BatchItemSaveChoice
    {
        SaveSuggested,
        SaveToOtherDeck,
        Skip
    }

    private sealed record SaveTargetSelection(
        bool ShouldSave,
        string? DeckFileName = null,
        string? DeckPath = null,
        string? OverridePartOfSpeech = null)
    {
        public static SaveTargetSelection Skip { get; } = new(false);
    }

    private sealed class PendingVocabularySave
    {
        public PendingVocabularySave(string requestedWord, string assistantReply, VocabularyAppendPreviewResult preview)
        {
            RequestedWord = requestedWord;
            AssistantReply = assistantReply;
            Preview = preview;
            TargetDeckFileName = preview.TargetDeckFileName ?? string.Empty;
            TargetDeckPath = preview.TargetDeckPath;
        }

        public string RequestedWord { get; }

        public string AssistantReply { get; }

        public VocabularyAppendPreviewResult Preview { get; }

        public string TargetDeckFileName { get; set; }

        public string? TargetDeckPath { get; set; }

        public string? OverridePartOfSpeech { get; set; }
    }

    private static async Task Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging
            .ClearProviders()
            .AddConsole()
            .SetMinimumLevel(LogLevel.Information);

        var sessionOptions = BuildSessionOptions(builder.Configuration);

        builder.Services
            .AddApplication(sessionOptions)
            .AddLagerthaAssistant(builder.Configuration);

        using var host = builder.Build();
        using var scope = host.Services.CreateScope();

        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

        try
        {
            var db = services.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrated and ready.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize database.");
            throw;
        }

        var aiOptions = services.GetRequiredService<OpenAiOptions>();
        var conversationOrchestrator = services.GetRequiredService<IConversationOrchestrator>();
        var vocabularyWorkflowService = services.GetRequiredService<IVocabularyWorkflowService>();
        var vocabularyDeckService = services.GetRequiredService<IVocabularyDeckService>();
        var vocabularyPersistenceService = services.GetRequiredService<IVocabularyPersistenceService>();
        var vocabularyStorageModeProvider = services.GetRequiredService<IVocabularyStorageModeProvider>();
        var graphAuthService = services.GetRequiredService<IGraphAuthService>();
        var userMemoryRepository = services.GetRequiredService<IUserMemoryRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();

        if (string.IsNullOrWhiteSpace(aiOptions.ApiKey))
        {
            Console.WriteLine($"{OpenAiConstants.ApiKeyEnvironmentVariable} is not configured.");
            Console.WriteLine($"Set environment variable {OpenAiConstants.ApiKeyEnvironmentVariable} and run again.");
            return;
        }

        await RunConsoleAssistantAsync(
            conversationOrchestrator,
            vocabularyWorkflowService,
            vocabularyDeckService,
            vocabularyPersistenceService,
            vocabularyStorageModeProvider,
            graphAuthService,
            userMemoryRepository,
            unitOfWork,
            aiOptions.Model);
    }

    private static async Task RunConsoleAssistantAsync(
        IConversationOrchestrator conversationOrchestrator,
        IVocabularyWorkflowService vocabularyWorkflowService,
        IVocabularyDeckService vocabularyDeckService,
        IVocabularyPersistenceService vocabularyPersistenceService,
        IVocabularyStorageModeProvider vocabularyStorageModeProvider,
        IGraphAuthService graphAuthService,
        IUserMemoryRepository userMemoryRepository,
        IUnitOfWork unitOfWork,
        string model)
    {
        PrintBanner(model);

        var uiScope = BuildUiScope();

        var saveMode = await LoadSaveModeAsync(userMemoryRepository, uiScope);
        PrintCurrentSaveMode(saveMode);

        var storageMode = await LoadStorageModeAsync(userMemoryRepository, vocabularyStorageModeProvider, uiScope);
        vocabularyStorageModeProvider.SetMode(storageMode);
        PrintCurrentStorageMode(vocabularyStorageModeProvider, storageMode);

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("You > ");
            Console.ResetColor();

            var input = Console.ReadLine();
            if (input is null)
            {
                break;
            }

            var command = input.Trim();

            var commandHandling = await TryHandleCommandAsync(
                command,
                saveMode,
                uiScope,
                conversationOrchestrator,
                vocabularyWorkflowService,
                vocabularyDeckService,
                vocabularyPersistenceService,
                vocabularyStorageModeProvider,
                graphAuthService,
                userMemoryRepository,
                unitOfWork);
            saveMode = commandHandling.SaveMode;

            if (commandHandling.ShouldExit)
            {
                break;
            }

            if (commandHandling.Handled)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(command))
            {
                continue;
            }

            try
            {
                var orchestrationResult = await conversationOrchestrator.ProcessAsync(
                    command,
                    uiScope.Channel,
                    uiScope.UserId,
                    uiScope.ConversationId);

                if (IsCommandResult(orchestrationResult))
                {
                    PrintCommandResult(orchestrationResult);
                    Console.WriteLine();
                    continue;
                }

                saveMode = await HandleVocabularyAgentResultAsync(
                    orchestrationResult,
                    vocabularyDeckService,
                    vocabularyPersistenceService,
                    saveMode);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        Console.WriteLine("Assistant session ended.");
    }

    private static ConversationScope BuildUiScope()
    {
        var userId = Environment.GetEnvironmentVariable(UiUserIdEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = Environment.UserName;
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            userId = "local-user";
        }

        var conversationId = Environment.GetEnvironmentVariable(UiConversationIdEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            conversationId = UiConversationId;
        }

        return ConversationScope.Create(UiChannel, userId, conversationId);
    }

    private static AssistantSessionOptions BuildSessionOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection("AssistantSession");

        var systemPrompt = section["SystemPrompt"];
        var maxHistoryRaw = section["MaxHistoryMessages"];

        var maxHistory = AssistantDefaults.MaxHistoryMessages;
        if (int.TryParse(maxHistoryRaw, out var parsed) && parsed > 1)
        {
            maxHistory = parsed;
        }

        return new AssistantSessionOptions
        {
            SystemPrompt = string.IsNullOrWhiteSpace(systemPrompt)
                ? AssistantDefaults.SystemPrompt
                : systemPrompt,
            MaxHistoryMessages = maxHistory
        };
    }
}
