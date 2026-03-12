using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.DependencyInjection;
using LagerthaAssistant.Application.Interfaces.AI;
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
        var assistantSession = services.GetRequiredService<IAssistantSessionService>();
        var conversationOrchestrator = services.GetRequiredService<IConversationOrchestrator>();
        var vocabularyWorkflowService = services.GetRequiredService<IVocabularyWorkflowService>();
        var vocabularyDeckService = services.GetRequiredService<IVocabularyDeckService>();
        var vocabularyPersistenceService = services.GetRequiredService<IVocabularyPersistenceService>();
        var vocabularySyncProcessor = services.GetRequiredService<IVocabularySyncProcessor>();
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
            assistantSession,
            conversationOrchestrator,
            vocabularyWorkflowService,
            vocabularyDeckService,
            vocabularyPersistenceService,
            vocabularySyncProcessor,
            vocabularyStorageModeProvider,
            graphAuthService,
            userMemoryRepository,
            unitOfWork,
            aiOptions.Model);
    }

    private static async Task RunConsoleAssistantAsync(
        IAssistantSessionService assistantSession,
        IConversationOrchestrator conversationOrchestrator,
        IVocabularyWorkflowService vocabularyWorkflowService,
        IVocabularyDeckService vocabularyDeckService,
        IVocabularyPersistenceService vocabularyPersistenceService,
        IVocabularySyncProcessor vocabularySyncProcessor,
        IVocabularyStorageModeProvider vocabularyStorageModeProvider,
        IGraphAuthService graphAuthService,
        IUserMemoryRepository userMemoryRepository,
        IUnitOfWork unitOfWork,
        string model)
    {
        PrintBanner(model);

        var saveMode = await LoadSaveModeAsync(userMemoryRepository);
        PrintCurrentSaveMode(saveMode);

        var storageMode = await LoadStorageModeAsync(userMemoryRepository, vocabularyStorageModeProvider);
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

            if (command.Equals(ConsoleCommands.Help, StringComparison.OrdinalIgnoreCase))
            {
                PrintHelp();
                continue;
            }

            if (command.Equals(ConsoleCommands.Batch, StringComparison.OrdinalIgnoreCase))
            {
                var rawBatchInput = ReadBatchInput();
                if (rawBatchInput is null)
                {
                    Console.WriteLine("Batch input cancelled.");
                    continue;
                }

                var parsedItems = VocabularyBatchInputParser.Parse(rawBatchInput);

                if (ShouldOfferSpaceSplit(rawBatchInput, parsedItems))
                {
                    var splitCandidates = SplitBatchInputBySpaces(parsedItems[0]);
                    if (splitCandidates.Count > 1 && AskBatchSpaceSplitChoice(parsedItems[0], splitCandidates))
                    {
                        parsedItems = splitCandidates;
                    }
                }
                if (parsedItems.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("warning: No valid items detected in batch input.");
                    Console.ResetColor();
                    continue;
                }

                PrintBatchItems(parsedItems);
                if (!AskYesNo("Process these items now?"))
                {
                    Console.WriteLine("Batch processing cancelled.");
                    continue;
                }

                saveMode = await ProcessBatchInputsAsync(
                    parsedItems,
                    vocabularyWorkflowService,
                    vocabularyDeckService,
                    vocabularyPersistenceService,
                    saveMode);
                continue;
            }

            if (command.Equals(ConsoleCommands.Save, StringComparison.OrdinalIgnoreCase))
            {
                PrintCurrentSaveMode(saveMode);
                continue;
            }

            if (command.Equals(ConsoleCommands.SaveMode, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Usage: /save mode ask|auto|off");
                PrintCurrentSaveMode(saveMode);
                continue;
            }

            if (command.StartsWith(ConsoleCommands.SaveMode + " ", StringComparison.OrdinalIgnoreCase))
            {
                var modeText = command[ConsoleCommands.SaveMode.Length..].Trim();
                if (!TryParseSaveMode(modeText, out var updatedSaveMode))
                {
                    Console.WriteLine("Usage: /save mode ask|auto|off");
                    continue;
                }

                saveMode = updatedSaveMode;
                await PersistSaveModeAsync(userMemoryRepository, unitOfWork, saveMode);
                PrintCurrentSaveMode(saveMode);
                continue;
            }

            if (command.Equals(ConsoleCommands.Storage, StringComparison.OrdinalIgnoreCase))
            {
                PrintCurrentStorageMode(vocabularyStorageModeProvider, vocabularyStorageModeProvider.CurrentMode);
                continue;
            }

            if (command.Equals(ConsoleCommands.StorageMode, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Usage: /storage mode local|graph");
                PrintCurrentStorageMode(vocabularyStorageModeProvider, vocabularyStorageModeProvider.CurrentMode);
                continue;
            }

            if (command.StartsWith(ConsoleCommands.StorageMode + " ", StringComparison.OrdinalIgnoreCase))
            {
                var modeText = command[ConsoleCommands.StorageMode.Length..].Trim();
                if (!vocabularyStorageModeProvider.TryParse(modeText, out var updatedStorageMode))
                {
                    Console.WriteLine("Usage: /storage mode local|graph");
                    continue;
                }

                vocabularyStorageModeProvider.SetMode(updatedStorageMode);
                await PersistStorageModeAsync(userMemoryRepository, unitOfWork, updatedStorageMode, vocabularyStorageModeProvider);
                PrintCurrentStorageMode(vocabularyStorageModeProvider, updatedStorageMode);

                if (updatedStorageMode == VocabularyStorageMode.Graph)
                {
                    var status = await graphAuthService.GetStatusAsync();
                    PrintGraphStatus(status);
                }

                continue;
            }

            if (command.Equals(ConsoleCommands.GraphStatus, StringComparison.OrdinalIgnoreCase))
            {
                var status = await graphAuthService.GetStatusAsync();
                PrintGraphStatus(status);
                continue;
            }

            if (command.Equals(ConsoleCommands.GraphLogin, StringComparison.OrdinalIgnoreCase))
            {
                var login = await graphAuthService.LoginAsync();
                Console.WriteLine(login.Message);

                if (login.Succeeded)
                {
                    if (vocabularyStorageModeProvider.CurrentMode != VocabularyStorageMode.Graph
                        && AskYesNo("Switch storage mode to graph now?"))
                    {
                        vocabularyStorageModeProvider.SetMode(VocabularyStorageMode.Graph);
                        await PersistStorageModeAsync(
                            userMemoryRepository,
                            unitOfWork,
                            VocabularyStorageMode.Graph,
                            vocabularyStorageModeProvider);
                        PrintCurrentStorageMode(vocabularyStorageModeProvider, VocabularyStorageMode.Graph);
                    }

                    var status = await graphAuthService.GetStatusAsync();
                    PrintGraphStatus(status);
                }

                continue;
            }

            if (command.Equals(ConsoleCommands.GraphLogout, StringComparison.OrdinalIgnoreCase))
            {
                await graphAuthService.LogoutAsync();
                Console.WriteLine("Graph token cache cleared.");
                var status = await graphAuthService.GetStatusAsync();
                PrintGraphStatus(status);
                continue;
            }

            if (command.Equals(ConsoleCommands.Sync, StringComparison.OrdinalIgnoreCase)
                || command.Equals(ConsoleCommands.SyncStatus, StringComparison.OrdinalIgnoreCase))
            {
                var pendingCount = await vocabularySyncProcessor.GetPendingCountAsync();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"info: Pending vocabulary sync jobs: {pendingCount}");
                Console.ResetColor();
                continue;
            }

            if (command.Equals(ConsoleCommands.SyncRun, StringComparison.OrdinalIgnoreCase)
                || command.StartsWith(ConsoleCommands.SyncRun + " ", StringComparison.OrdinalIgnoreCase))
            {
                var take = ParseSyncBatchSize(command);
                var summary = await vocabularySyncProcessor.ProcessPendingAsync(take);
                PrintSyncRunSummary(summary);
                continue;
            }
            if (command.Equals(ConsoleCommands.Exit, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (command.Equals(ConsoleCommands.Reset, StringComparison.OrdinalIgnoreCase))
            {
                assistantSession.Reset();
                Console.WriteLine("Conversation has been reset.");
                continue;
            }

            if (command.Equals(ConsoleCommands.History, StringComparison.OrdinalIgnoreCase))
            {
                var preview = await assistantSession.GetRecentHistoryAsync(ConsoleCommands.HistoryPreviewTake);
                PrintHistory(preview);
                continue;
            }

            if (command.Equals(ConsoleCommands.Memory, StringComparison.OrdinalIgnoreCase))
            {
                var memory = await assistantSession.GetActiveMemoryAsync(ConsoleCommands.MemoryPreviewTake);
                PrintMemory(memory);
                continue;
            }

            if (command.Equals(ConsoleCommands.Prompt, StringComparison.OrdinalIgnoreCase))
            {
                var prompt = await assistantSession.GetSystemPromptAsync();
                PrintSystemPrompt(prompt);
                continue;
            }

            if (command.Equals(ConsoleCommands.PromptDefault, StringComparison.OrdinalIgnoreCase))
            {
                var updated = await assistantSession.SetSystemPromptAsync(AssistantDefaults.SystemPrompt, "default");
                Console.WriteLine("System prompt reset to default and saved.");
                PrintSystemPrompt(updated);
                continue;
            }

            if (command.Equals(ConsoleCommands.PromptHistory, StringComparison.OrdinalIgnoreCase))
            {
                var history = await assistantSession.GetSystemPromptHistoryAsync(ConsoleCommands.PromptHistoryTake);
                PrintPromptHistory(history);
                continue;
            }

            if (command.Equals(ConsoleCommands.PromptProposals, StringComparison.OrdinalIgnoreCase))
            {
                var proposals = await assistantSession.GetSystemPromptProposalsAsync(ConsoleCommands.PromptProposalsTake);
                PrintPromptProposals(proposals);
                continue;
            }

            var setPrefix = $"{ConsoleCommands.Prompt} set";
            if (command.Equals(setPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var capturedPrompt = ReadMultilinePrompt();
                if (string.IsNullOrWhiteSpace(capturedPrompt))
                {
                    Console.WriteLine("Prompt update cancelled.");
                    continue;
                }

                var updated = await assistantSession.SetSystemPromptAsync(capturedPrompt, "manual");
                Console.WriteLine("System prompt updated and saved.");
                PrintSystemPrompt(updated);
                continue;
            }

            if (command.StartsWith(setPrefix + " ", StringComparison.OrdinalIgnoreCase))
            {
                var promptText = command[setPrefix.Length..].TrimStart();
                if (string.IsNullOrWhiteSpace(promptText))
                {
                    Console.WriteLine("Usage: /prompt set <new prompt text>");
                    continue;
                }

                var updated = await assistantSession.SetSystemPromptAsync(promptText, "manual");
                Console.WriteLine("System prompt updated and saved.");
                PrintSystemPrompt(updated);
                continue;
            }

            var proposePrefix = $"{ConsoleCommands.Prompt} propose";
            if (command.StartsWith(proposePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var payload = command.Length > proposePrefix.Length
                    ? command[proposePrefix.Length..].TrimStart()
                    : string.Empty;

                var split = payload.Split("||", 2, StringSplitOptions.TrimEntries);
                if (split.Length < 2 || string.IsNullOrWhiteSpace(split[0]) || string.IsNullOrWhiteSpace(split[1]))
                {
                    Console.WriteLine("Usage: /prompt propose <reason> || <new prompt text>");
                    continue;
                }

                var proposal = await assistantSession.CreateSystemPromptProposalAsync(split[1], split[0], 0.8, "manual");
                Console.WriteLine($"Proposal #{proposal.Id} has been saved with status '{proposal.Status}'.");
                continue;
            }

            var improvePrefix = $"{ConsoleCommands.Prompt} improve";
            if (command.StartsWith(improvePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var goal = command.Length > improvePrefix.Length
                    ? command[improvePrefix.Length..].TrimStart()
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(goal))
                {
                    Console.WriteLine("Usage: /prompt improve <goal>");
                    continue;
                }

                var proposal = await assistantSession.GenerateSystemPromptProposalAsync(goal);
                Console.WriteLine($"AI proposal #{proposal.Id} generated. Review via /prompt proposals and apply with /prompt apply <id>.");
                continue;
            }

            var applyPrefix = $"{ConsoleCommands.Prompt} apply";
            if (command.StartsWith(applyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var idText = command.Length > applyPrefix.Length
                    ? command[applyPrefix.Length..].TrimStart()
                    : string.Empty;

                if (!int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var proposalId) || proposalId <= 0)
                {
                    Console.WriteLine("Usage: /prompt apply <proposalId>");
                    continue;
                }

                var updated = await assistantSession.ApplySystemPromptProposalAsync(proposalId);
                Console.WriteLine($"Proposal #{proposalId} applied.");
                PrintSystemPrompt(updated);
                continue;
            }

            var rejectPrefix = $"{ConsoleCommands.Prompt} reject";
            if (command.StartsWith(rejectPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var idText = command.Length > rejectPrefix.Length
                    ? command[rejectPrefix.Length..].TrimStart()
                    : string.Empty;

                if (!int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var proposalId) || proposalId <= 0)
                {
                    Console.WriteLine("Usage: /prompt reject <proposalId>");
                    continue;
                }

                await assistantSession.RejectSystemPromptProposalAsync(proposalId);
                Console.WriteLine($"Proposal #{proposalId} rejected.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(command))
            {
                continue;
            }

            try
            {
                var orchestrationResult = await conversationOrchestrator.ProcessAsync(command, "ui");

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

    private static string? ReadBatchInput()
    {
        Console.WriteLine("Paste words, phrases, or sentences for batch processing.");
        Console.WriteLine("Rules: each line is an item; single-line input is auto-split by tab, ';', or sentence boundaries.");
        Console.WriteLine("Type /end on a new line to process, or /cancel to abort.");

        var lines = new List<string>();

        while (true)
        {
            var line = Console.ReadLine();
            if (line is null)
            {
                return null;
            }

            if (line.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (line.Equals("/end", StringComparison.OrdinalIgnoreCase))
            {
                var batchText = string.Join(Environment.NewLine, lines).Trim();
                return string.IsNullOrWhiteSpace(batchText)
                    ? null
                    : batchText;
            }

            lines.Add(line);
        }
    }

    private static bool ShouldOfferSpaceSplit(string rawBatchInput, IReadOnlyList<string> parsedItems)
    {
        if (parsedItems.Count != 1)
        {
            return false;
        }

        var normalized = rawBatchInput.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (normalized.Contains(Environment.NewLine, StringComparison.Ordinal)
            || normalized.Contains('\n')
            || normalized.Contains('\r')
            || normalized.Contains('\t')
            || normalized.Contains(';')
            || normalized.Contains(',')
            || normalized.Contains('.')
            || normalized.Contains('!')
            || normalized.Contains('?'))
        {
            return false;
        }

        return normalized.Contains(' ', StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> SplitBatchInputBySpaces(string input)
    {
        var tokens = input
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return tokens;
    }

    private static bool AskBatchSpaceSplitChoice(string originalItem, IReadOnlyList<string> splitCandidates)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Detected one item without separators.");
        Console.ResetColor();
        Console.WriteLine($"Input: {originalItem}");
        Console.WriteLine($"Potential split ({splitCandidates.Count} items): {string.Join(", ", splitCandidates)}");
        Console.WriteLine("1) Keep as one phrase/sentence");
        Console.WriteLine("2) Split by spaces into separate words");

        while (true)
        {
            Console.Write("Select [1/2]: ");
            var answer = Console.ReadLine()?.Trim().ToLowerInvariant() ?? string.Empty;

            if (answer is "1" or "keep" or "k")
            {
                return false;
            }

            if (answer is "2" or "split" or "s")
            {
                return true;
            }

            Console.WriteLine("Please enter 1 or 2.");
        }
    }
    private static void PrintBatchItems(IReadOnlyList<string> items)
    {
        Console.WriteLine();
        Console.WriteLine("Detected batch items:");

        for (var index = 0; index < items.Count; index++)
        {
            Console.WriteLine($"{index + 1}) {items[index]}");
        }

        Console.WriteLine();
    }
    private static void PrintVocabularyFromDeck(VocabularyLookupResult lookup)
    {
        Console.WriteLine();
        Console.WriteLine("Found in writable decks:");

        foreach (var match in lookup.Matches)
        {
            Console.WriteLine($"- {match.DeckFileName} (row {match.RowNumber})");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Assistant ({match.DeckFileName}) > {match.Word}");
            Console.WriteLine();
            Console.WriteLine(match.Meaning);
            Console.WriteLine();
            Console.WriteLine(match.Examples);
            Console.ResetColor();

            Console.WriteLine();
        }
    }

    private static void PrintVocabularyAppendResult(VocabularyAppendResult result)
    {
        switch (result.Status)
        {
            case VocabularyAppendStatus.Added when result.Entry is not null:
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"info: Saved to {result.Entry.DeckFileName} (row {result.Entry.RowNumber}).");
                Console.ResetColor();
                break;

            case VocabularyAppendStatus.DuplicateFound:
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("info: Word already exists in writable decks. Save skipped.");
                Console.ResetColor();
                break;

            case VocabularyAppendStatus.ParseFailed:
            case VocabularyAppendStatus.NoWritableDecks:
            case VocabularyAppendStatus.NoMatchingDeck:
            case VocabularyAppendStatus.Error:
                if (!string.IsNullOrWhiteSpace(result.Message))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"warning: {result.Message}");
                    Console.ResetColor();
                }
                break;
        }
    }

    private static void PrintVocabularyAppendPreviewResult(VocabularyAppendPreviewResult result)
    {
        switch (result.Status)
        {
            case VocabularyAppendPreviewStatus.DuplicateFound:
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("info: Word already exists in writable decks. Save skipped.");
                Console.ResetColor();
                break;

            case VocabularyAppendPreviewStatus.ParseFailed:
            case VocabularyAppendPreviewStatus.NoWritableDecks:
            case VocabularyAppendPreviewStatus.NoMatchingDeck:
            case VocabularyAppendPreviewStatus.Error:
                if (!string.IsNullOrWhiteSpace(result.Message))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"warning: {result.Message}");
                    Console.ResetColor();
                }
                break;
        }
    }

    private static SaveConfirmationChoice AskVocabularySaveConfirmation(VocabularyAppendPreviewResult preview)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Save \"{preview.Word}\" to \"{preview.TargetDeckFileName}\"?");
        Console.ResetColor();
        Console.WriteLine($"Target path: {preview.TargetDeckPath}");
        Console.WriteLine("1) Yes");
        Console.WriteLine("2) Yes, and don't ask again in this session");
        Console.WriteLine("3) No");
        Console.WriteLine("4) Save to another deck with custom POS marker");

        while (true)
        {
            Console.Write("Select [1/2/3/4]: ");
            var answer = Console.ReadLine()?.Trim().ToLowerInvariant() ?? string.Empty;

            if (answer is "1" or "y" or "yes")
            {
                return SaveConfirmationChoice.Yes;
            }

            if (answer is "2" or "a" or "always")
            {
                return SaveConfirmationChoice.YesDontAskAgain;
            }

            if (answer is "3" or "n" or "no")
            {
                return SaveConfirmationChoice.No;
            }

            if (answer is "4" or "c" or "custom")
            {
                return SaveConfirmationChoice.SaveToOtherDeck;
            }

            Console.WriteLine("Please enter 1, 2, 3, or 4.");
        }
    }

    private static async Task<SaveTargetSelection> AskCustomSaveTargetAsync(
        IVocabularyDeckService vocabularyDeckService,
        VocabularyAppendPreviewResult preview)
    {
        var writableDecks = await vocabularyDeckService.GetWritableDeckFilesAsync();
        if (writableDecks.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("warning: No writable decks available for custom save.");
            Console.ResetColor();
            return SaveTargetSelection.Skip;
        }

        Console.WriteLine("Choose writable target deck:");
        for (var index = 0; index < writableDecks.Count; index++)
        {
            var deck = writableDecks[index];
            var isSuggested = deck.FileName.Equals(preview.TargetDeckFileName, StringComparison.OrdinalIgnoreCase);
            if (isSuggested)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"{index + 1}) {deck.FileName} (suggested)");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($"{index + 1}) {deck.FileName}");
            }
        }

        var selectedDeck = AskDeckSelection(writableDecks);
        if (selectedDeck is null)
        {
            return SaveTargetSelection.Skip;
        }

        var suggestedMarker = GetSuggestedPosMarkerForDeckFileName(selectedDeck.FileName);
        var markerValue = AskPartOfSpeechMarker(suggestedMarker);
        if (string.IsNullOrWhiteSpace(markerValue))
        {
            return SaveTargetSelection.Skip;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Save \"{preview.Word}\" to \"{selectedDeck.FileName}\" with marker \"({markerValue})\"?");
        Console.ResetColor();
        Console.WriteLine($"Target path: {selectedDeck.FullPath}");

        return AskYesNo("Save this card to the selected deck?")
            ? new SaveTargetSelection(true, selectedDeck.FileName, selectedDeck.FullPath, markerValue)
            : SaveTargetSelection.Skip;
    }

    private static VocabularyDeckFile? AskDeckSelection(IReadOnlyList<VocabularyDeckFile> writableDecks)
    {
        while (true)
        {
            Console.Write($"Select [1-{writableDecks.Count}] or 0 to cancel: ");
            var answer = Console.ReadLine()?.Trim() ?? string.Empty;

            if (answer is "0"
                || answer.Equals("no", StringComparison.OrdinalIgnoreCase)
                || answer.Equals("n", StringComparison.OrdinalIgnoreCase)
                || answer.Equals("cancel", StringComparison.OrdinalIgnoreCase)
                || answer.Equals("c", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (int.TryParse(answer, NumberStyles.Integer, CultureInfo.InvariantCulture, out var selectedIndex)
                && selectedIndex >= 1
                && selectedIndex <= writableDecks.Count)
            {
                return writableDecks[selectedIndex - 1];
            }

            Console.WriteLine($"Please enter a number from 1 to {writableDecks.Count}, or 0 to cancel.");
        }
    }

    private static string? AskPartOfSpeechMarker(string? suggestedMarker = null)
    {
        Console.WriteLine("Choose POS marker:");
        WritePosMarkerOption("1", "n", "noun", suggestedMarker);
        WritePosMarkerOption("2", "v", "verb", suggestedMarker);
        WritePosMarkerOption("3", "iv", "irregular verb", suggestedMarker);
        WritePosMarkerOption("4", "pv", "phrasal verb", suggestedMarker);
        WritePosMarkerOption("5", "adj", "adjective", suggestedMarker);
        WritePosMarkerOption("6", "adv", "adverb", suggestedMarker);
        WritePosMarkerOption("7", "prep", "preposition", suggestedMarker);
        WritePosMarkerOption("8", "conj", "conjunction", suggestedMarker);
        WritePosMarkerOption("9", "pron", "pronoun", suggestedMarker);
        WritePosMarkerOption("10", "pe", "persistent expression", suggestedMarker);
        Console.WriteLine("0) Cancel");

        while (true)
        {
            Console.Write("Select [1/2/3/4/5/6/7/8/9/10/0] or type marker: ");
            var answer = Console.ReadLine()?.Trim() ?? string.Empty;

            if (answer is "0"
                || answer.Equals("no", StringComparison.OrdinalIgnoreCase)
                || answer.Equals("n", StringComparison.OrdinalIgnoreCase)
                || answer.Equals("cancel", StringComparison.OrdinalIgnoreCase)
                || answer.Equals("c", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (TryNormalizePartOfSpeechMarker(answer, out var marker))
            {
                return marker;
            }

            Console.WriteLine("Unsupported marker. Use 1..10 or one of: n, v, iv, pv, adj, adv, prep, conj, pron, pe.");
        }
    }

    private static void WritePosMarkerOption(string number, string marker, string label, string? suggestedMarker)
    {
        var isSuggested = !string.IsNullOrWhiteSpace(suggestedMarker)
            && marker.Equals(suggestedMarker, StringComparison.OrdinalIgnoreCase);

        var optionText = isSuggested
            ? $"{number}) {marker} ({label}) (suggested)"
            : $"{number}) {marker} ({label})";

        if (isSuggested)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(optionText);
            Console.ResetColor();
            return;
        }

        Console.WriteLine(optionText);
    }

    private static string? GetSuggestedPosMarkerForDeckFileName(string deckFileName)
    {
        if (string.IsNullOrWhiteSpace(deckFileName))
        {
            return null;
        }

        var name = deckFileName.ToLowerInvariant();

        if ((name.Contains("persistent", StringComparison.Ordinal) || name.Contains("persistant", StringComparison.Ordinal))
            && name.Contains("expression", StringComparison.Ordinal))
        {
            return "pe";
        }

        if (name.Contains("phrasal", StringComparison.Ordinal))
        {
            return "pv";
        }

        if (name.Contains("irregular", StringComparison.Ordinal) && name.Contains("verb", StringComparison.Ordinal))
        {
            return "iv";
        }

        if (name.Contains("verb", StringComparison.Ordinal))
        {
            return "v";
        }

        if (name.Contains("noun", StringComparison.Ordinal))
        {
            return "n";
        }

        if (name.Contains("adjective", StringComparison.Ordinal))
        {
            return "adj";
        }

        if (name.Contains("adverb", StringComparison.Ordinal))
        {
            return "adv";
        }

        if (name.Contains("preposition", StringComparison.Ordinal))
        {
            return "prep";
        }

        if (name.Contains("conjunction", StringComparison.Ordinal))
        {
            return "conj";
        }

        if (name.Contains("pronoun", StringComparison.Ordinal))
        {
            return "pron";
        }

        return null;
    }

    private static bool AskYesNo(string question)
    {
        Console.WriteLine(question);
        Console.WriteLine("1) Yes");
        Console.WriteLine("2) No");

        while (true)
        {
            Console.Write("Select [1/2]: ");
            var answer = Console.ReadLine()?.Trim().ToLowerInvariant() ?? string.Empty;

            if (answer is "1" or "y" or "yes")
            {
                return true;
            }

            if (answer is "2" or "n" or "no")
            {
                return false;
            }

            Console.WriteLine("Please enter 1 or 2 (or yes/no).");
        }
    }

    private static bool TryNormalizePartOfSpeechMarker(string value, out string marker)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "n":
            case "1":
            case "noun":
                marker = "n";
                return true;
            case "v":
            case "2":
            case "verb":
                marker = "v";
                return true;
            case "iv":
            case "3":
            case "irregular":
            case "irregular-verb":
                marker = "iv";
                return true;
            case "pv":
            case "4":
            case "phrasal":
            case "phrasal-verb":
                marker = "pv";
                return true;
            case "adj":
            case "5":
            case "adjective":
                marker = "adj";
                return true;
            case "adv":
            case "6":
            case "adverb":
                marker = "adv";
                return true;
            case "prep":
            case "7":
            case "preposition":
                marker = "prep";
                return true;
            case "conj":
            case "8":
            case "conjunction":
                marker = "conj";
                return true;
            case "pron":
            case "9":
            case "pronoun":
                marker = "pron";
                return true;
            case "pe":
            case "10":
            case "persistent":
            case "persistent-expression":
            case "persistant-expression":
            case "expression":
                marker = "pe";
                return true;
            default:
                marker = string.Empty;
                return false;
        }
    }

    private static bool AskRetrySaveConfirmation(VocabularyAppendPreviewResult preview)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Retry saving \"{preview.Word}\" to \"{preview.TargetDeckFileName}\" now?");
        Console.ResetColor();

        return AskYesNo("Close the file first, then choose:");
    }

    private static bool IsFileLockedSaveError(VocabularyAppendResult result)
    {
        if (result.Status != VocabularyAppendStatus.Error || string.IsNullOrWhiteSpace(result.Message))
        {
            return false;
        }

        return result.Message.Contains("file is open in another app", StringComparison.OrdinalIgnoreCase)
            || result.Message.Contains("currently in use", StringComparison.OrdinalIgnoreCase)
            || result.Message.Contains("file is locked", StringComparison.OrdinalIgnoreCase)
            || result.Message.Contains("locked right now", StringComparison.OrdinalIgnoreCase);
    }
    private static async Task<SaveMode> LoadSaveModeAsync(IUserMemoryRepository userMemoryRepository)
    {
        var entry = await userMemoryRepository.GetByKeyAsync(SaveModeMemoryKey);
        if (entry is null)
        {
            return SaveMode.Ask;
        }

        return TryParseSaveMode(entry.Value, out var saveMode)
            ? saveMode
            : SaveMode.Ask;
    }

    private static async Task PersistSaveModeAsync(
        IUserMemoryRepository userMemoryRepository,
        IUnitOfWork unitOfWork,
        SaveMode saveMode)
    {
        var modeValue = ToSaveModeText(saveMode);
        var now = DateTimeOffset.UtcNow;

        var entry = await userMemoryRepository.GetByKeyAsync(SaveModeMemoryKey);
        if (entry is null)
        {
            await userMemoryRepository.AddAsync(new UserMemoryEntry
            {
                Key = SaveModeMemoryKey,
                Value = modeValue,
                Confidence = 1.0,
                IsActive = false,
                LastSeenAtUtc = now
            });
        }
        else
        {
            entry.Value = modeValue;
            entry.Confidence = 1.0;
            entry.IsActive = false;
            entry.LastSeenAtUtc = now;
        }

        await unitOfWork.SaveChangesAsync();
    }

    private static bool TryParseSaveMode(string? value, out SaveMode saveMode)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "ask":
                saveMode = SaveMode.Ask;
                return true;
            case "auto":
                saveMode = SaveMode.Auto;
                return true;
            case "off":
                saveMode = SaveMode.Off;
                return true;
            default:
                saveMode = SaveMode.Ask;
                return false;
        }
    }

    private static string ToSaveModeText(SaveMode saveMode)
    {
        return saveMode switch
        {
            SaveMode.Ask => "ask",
            SaveMode.Auto => "auto",
            SaveMode.Off => "off",
            _ => "ask"
        };
    }

    private static void PrintCurrentSaveMode(SaveMode saveMode)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"info: Save mode is '{ToSaveModeText(saveMode)}'.");
        Console.ResetColor();
    }

    private static async Task<VocabularyStorageMode> LoadStorageModeAsync(
        IUserMemoryRepository userMemoryRepository,
        IVocabularyStorageModeProvider vocabularyStorageModeProvider)
    {
        var entry = await userMemoryRepository.GetByKeyAsync(StorageModeMemoryKey);
        if (entry is null)
        {
            return vocabularyStorageModeProvider.CurrentMode;
        }

        return vocabularyStorageModeProvider.TryParse(entry.Value, out var mode)
            ? mode
            : vocabularyStorageModeProvider.CurrentMode;
    }

    private static async Task PersistStorageModeAsync(
        IUserMemoryRepository userMemoryRepository,
        IUnitOfWork unitOfWork,
        VocabularyStorageMode mode,
        IVocabularyStorageModeProvider vocabularyStorageModeProvider)
    {
        var modeValue = vocabularyStorageModeProvider.ToText(mode);
        var now = DateTimeOffset.UtcNow;

        var entry = await userMemoryRepository.GetByKeyAsync(StorageModeMemoryKey);
        if (entry is null)
        {
            await userMemoryRepository.AddAsync(new UserMemoryEntry
            {
                Key = StorageModeMemoryKey,
                Value = modeValue,
                Confidence = 1.0,
                IsActive = false,
                LastSeenAtUtc = now
            });
        }
        else
        {
            entry.Value = modeValue;
            entry.Confidence = 1.0;
            entry.IsActive = false;
            entry.LastSeenAtUtc = now;
        }

        await unitOfWork.SaveChangesAsync();
    }

    private static void PrintCurrentStorageMode(
        IVocabularyStorageModeProvider vocabularyStorageModeProvider,
        VocabularyStorageMode mode)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"info: Storage mode is '{vocabularyStorageModeProvider.ToText(mode)}'.");
        Console.ResetColor();
    }

    private static void PrintGraphStatus(GraphAuthStatus status)
    {
        if (!status.IsConfigured)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"warning: {status.Message}");
            Console.ResetColor();
            return;
        }

        if (status.IsAuthenticated)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"info: Graph status: authenticated. Token expires at {status.AccessTokenExpiresAtUtc:yyyy-MM-dd HH:mm:ss} UTC.");
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"warning: Graph status: {status.Message}");
        Console.ResetColor();
    }
    private static int ParseSyncBatchSize(string command)
    {
        if (command.Equals(ConsoleCommands.SyncRun, StringComparison.OrdinalIgnoreCase))
        {
            return 25;
        }

        var suffix = command[ConsoleCommands.SyncRun.Length..].Trim();
        if (int.TryParse(suffix, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("warning: Invalid batch size. Using default 25.");
        Console.ResetColor();
        return 25;
    }

    private static void PrintSyncRunSummary(VocabularySyncRunSummary summary)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"info: Sync run processed {summary.Processed}/{summary.Requested} job(s): completed={summary.Completed}, requeued={summary.Requeued}, failed={summary.Failed}, pending={summary.PendingAfterRun}.");
        Console.ResetColor();
    }
    private static string? ReadMultilinePrompt()
    {
        Console.WriteLine("Paste the system prompt below.");
        Console.WriteLine("Type /end on a new line to save, or /cancel to abort.");

        var lines = new List<string>();

        while (true)
        {
            var line = Console.ReadLine();
            if (line is null)
            {
                return null;
            }

            if (line.Equals("/end", StringComparison.OrdinalIgnoreCase))
            {
                var prompt = string.Join(Environment.NewLine, lines).Trim();
                return string.IsNullOrWhiteSpace(prompt) ? null : prompt;
            }

            if (line.Equals("/cancel", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            lines.Add(line);
        }
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



































