using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.DependencyInjection;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Vocabulary;
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

internal static class Program
{
    private const string SaveModeMemoryKey = "ui.save.mode";

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

    private sealed record SaveTargetSelection(
        bool ShouldSave,
        string? DeckFileName = null,
        string? DeckPath = null,
        string? OverridePartOfSpeech = null)
    {
        public static SaveTargetSelection Skip { get; } = new(false);
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
        var vocabularyDeckService = services.GetRequiredService<IVocabularyDeckService>();
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
            vocabularyDeckService,
            userMemoryRepository,
            unitOfWork,
            aiOptions.Model);
    }

    private static async Task RunConsoleAssistantAsync(
        IAssistantSessionService assistantSession,
        IVocabularyDeckService vocabularyDeckService,
        IUserMemoryRepository userMemoryRepository,
        IUnitOfWork unitOfWork,
        string model)
    {
        PrintBanner(model);

        var saveMode = await LoadSaveModeAsync(userMemoryRepository);
        PrintCurrentSaveMode(saveMode);

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
                var lookup = await vocabularyDeckService.FindInWritableDecksAsync(command);
                if (lookup.Found)
                {
                    PrintVocabularyFromDeck(lookup);
                    continue;
                }

                var result = await assistantSession.AskAsync(command);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Assistant ({result.Model}) > {result.Content}");
                Console.ResetColor();

                var preview = await vocabularyDeckService.PreviewAppendFromAssistantReplyAsync(command, result.Content);

                if (preview.Status == VocabularyAppendPreviewStatus.ReadyToAppend
                    && !string.IsNullOrWhiteSpace(preview.TargetDeckFileName)
                    && !string.IsNullOrWhiteSpace(preview.TargetDeckPath))
                {
                    var shouldSave = saveMode != SaveMode.Off;
                    var targetDeckFileName = preview.TargetDeckFileName;
                    var targetDeckPath = preview.TargetDeckPath;
                    string? overridePartOfSpeech = null;
                    var retryPreview = preview;

                    if (saveMode == SaveMode.Ask)
                    {
                        var confirmationChoice = AskVocabularySaveConfirmation(preview);
                        shouldSave = confirmationChoice != SaveConfirmationChoice.No;

                        if (confirmationChoice == SaveConfirmationChoice.YesDontAskAgain)
                        {
                            saveMode = SaveMode.Auto;
                        }
                        else if (confirmationChoice == SaveConfirmationChoice.SaveToOtherDeck)
                        {
                            var customSave = await AskCustomSaveTargetAsync(vocabularyDeckService, preview);
                            shouldSave = customSave.ShouldSave;

                            if (customSave.ShouldSave)
                            {
                                targetDeckFileName = customSave.DeckFileName;
                                targetDeckPath = customSave.DeckPath;
                                overridePartOfSpeech = customSave.OverridePartOfSpeech;
                                retryPreview = preview with
                                {
                                    TargetDeckFileName = customSave.DeckFileName,
                                    TargetDeckPath = customSave.DeckPath
                                };
                            }
                        }
                    }

                    if (shouldSave && !string.IsNullOrWhiteSpace(targetDeckFileName))
                    {
                        var appendResult = await vocabularyDeckService.AppendFromAssistantReplyAsync(
                            command,
                            result.Content,
                            targetDeckFileName,
                            overridePartOfSpeech);
                        PrintVocabularyAppendResult(appendResult);

                        while (IsFileLockedSaveError(appendResult) && AskRetrySaveConfirmation(retryPreview))
                        {
                            appendResult = await vocabularyDeckService.AppendFromAssistantReplyAsync(
                                command,
                                result.Content,
                                targetDeckFileName,
                                overridePartOfSpeech);
                            PrintVocabularyAppendResult(appendResult);
                        }
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("info: Save skipped (mode=off or declined by user).");
                        Console.ResetColor();
                    }
                }
                else
                {
                    PrintVocabularyAppendPreviewResult(preview);
                }

                if (result.Usage is not null)
                {
                    Console.WriteLine(
                        $"Tokens: prompt={result.Usage.PromptTokens}, completion={result.Usage.CompletionTokens}, total={result.Usage.TotalTokens}");
                }

                Console.WriteLine();
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
        Console.WriteLine("0) Cancel");

        while (true)
        {
            Console.Write("Select [1/2/3/4/5/6/7/8/9/0] or type marker: ");
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

            Console.WriteLine("Unsupported marker. Use 1..9 or one of: n, v, iv, pv, adj, adv, prep, conj, pron.");
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
            || result.Message.Contains("currently in use", StringComparison.OrdinalIgnoreCase);
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

    private static void PrintBanner(string model)
    {
        Console.WriteLine(new string('=', 72));
        Console.WriteLine("OpenAI Console Assistant Prototype");
        Console.WriteLine($"Model: {model}");
        Console.WriteLine("Type /help to see all commands.");
        Console.WriteLine(new string('=', 72));
        Console.WriteLine();
    }

    private static void PrintHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Available commands:");
        Console.WriteLine();

        Console.WriteLine("General");
        WriteCommandHelp(ConsoleCommands.Help, "Show this help message.");

        Console.WriteLine();
        Console.WriteLine("Conversation");
        WriteCommandHelp(ConsoleCommands.History, "Show recent conversation history.");
        WriteCommandHelp(ConsoleCommands.Memory, "Show active memory facts.");

        Console.WriteLine();
        Console.WriteLine("System prompt");
        WriteCommandHelp(ConsoleCommands.Prompt, "Show the active system prompt.");
        WriteCommandHelp(ConsoleCommands.PromptDefault, "Reset active system prompt to default and save it.");
        WriteCommandHelp(ConsoleCommands.PromptHistory, "Show saved system prompt versions.");
        WriteCommandHelp("/prompt set", "Start multiline prompt editor (finish with /end, cancel with /cancel).");
        WriteCommandHelp("/prompt set <text>", "Set system prompt from a single line.");

        Console.WriteLine();
        Console.WriteLine("Prompt proposals");
        WriteCommandHelp(ConsoleCommands.PromptProposals, "Show recent prompt proposals.");
        WriteCommandHelp("/prompt propose <reason> || <text>", "Create a manual proposal for a new prompt.");
        WriteCommandHelp("/prompt improve <goal>", "Ask AI to generate a prompt proposal for your goal.");
        WriteCommandHelp("/prompt apply <id>", "Apply a pending proposal and make it active.");
        WriteCommandHelp("/prompt reject <id>", "Reject a pending proposal.");

        Console.WriteLine();
        Console.WriteLine("Saving");
        WriteCommandHelp(ConsoleCommands.Save, "Show current save mode (ask|auto|off).");
        WriteCommandHelp("/save mode ask", "Ask before every save (default). Stored in DB.");
        WriteCommandHelp("/save mode auto", "Save automatically without confirmation. Stored in DB.");
        WriteCommandHelp("/save mode off", "Never save automatically. Stored in DB.");
        Console.WriteLine("In ask mode, option 4 lets you pick another writable deck and override the POS marker.");
        Console.WriteLine("Custom save quick example: 4 -> choose deck number -> enter marker (e.g. prep) -> confirm.");
        Console.WriteLine("All interactive prompts accept digits and text (example: 1 or yes).");

        Console.WriteLine();
        Console.WriteLine("Session");
        WriteCommandHelp(ConsoleCommands.Reset, "Reset in-memory conversation context.");
        WriteCommandHelp(ConsoleCommands.Exit, "Exit the application.");

        Console.WriteLine();
        Console.WriteLine("Vocabulary flow");
        Console.WriteLine("Type an English word/phrase to check writable decks first, then AI answer and save based on current save mode.");
        Console.WriteLine("What it does: detects phrasal verbs as (pv), normalizes irregular verbs to 3 forms as (iv),");
        Console.WriteLine("checks duplicates in writable decks, and saves card data to the best deck or your custom target.");
        Console.WriteLine();
    }

    private static void WriteCommandHelp(string commandText, string description)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(commandText);
        Console.ResetColor();
        Console.Write(" - ");
        Console.WriteLine(description);
    }

    private static void PrintHistory(IReadOnlyCollection<ConversationMessage> messages)
    {
        Console.WriteLine();
        Console.WriteLine("Conversation history:");

        if (messages.Count == 0)
        {
            Console.WriteLine("- empty");
            Console.WriteLine();
            return;
        }

        foreach (var message in messages)
        {
            Console.WriteLine($"- {message.Role}: {message.Content}");
        }

        Console.WriteLine();
    }

    private static void PrintMemory(IReadOnlyCollection<UserMemoryEntry> memoryEntries)
    {
        Console.WriteLine();
        Console.WriteLine("Stored memory:");

        if (memoryEntries.Count == 0)
        {
            Console.WriteLine("- empty");
            Console.WriteLine();
            return;
        }

        foreach (var memory in memoryEntries)
        {
            Console.WriteLine($"- {memory.Key}: {memory.Value} (confidence={memory.Confidence:F2}, seen={memory.LastSeenAtUtc:yyyy-MM-dd HH:mm:ss} UTC)");
        }

        Console.WriteLine();
    }

    private static void PrintSystemPrompt(string prompt)
    {
        Console.WriteLine();
        Console.WriteLine("Active system prompt:");
        Console.WriteLine(prompt);
        Console.WriteLine();
    }

    private static void PrintPromptHistory(IReadOnlyCollection<SystemPromptEntry> history)
    {
        Console.WriteLine();
        Console.WriteLine("System prompt history:");

        if (history.Count == 0)
        {
            Console.WriteLine("- empty");
            Console.WriteLine();
            return;
        }

        foreach (var item in history)
        {
            var activeFlag = item.IsActive ? " [active]" : string.Empty;
            Console.WriteLine($"- v{item.Version}{activeFlag} source={item.Source} created={item.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"  {item.PromptText}");
        }

        Console.WriteLine();
    }

    private static void PrintPromptProposals(IReadOnlyCollection<SystemPromptProposal> proposals)
    {
        Console.WriteLine();
        Console.WriteLine("System prompt proposals:");

        if (proposals.Count == 0)
        {
            Console.WriteLine("- empty");
            Console.WriteLine();
            return;
        }

        foreach (var item in proposals)
        {
            Console.WriteLine($"- #{item.Id} status={item.Status} source={item.Source} confidence={item.Confidence:F2} created={item.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"  reason: {item.Reason}");
            Console.WriteLine($"  prompt: {item.ProposedPrompt}");
        }

        Console.WriteLine();
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


















