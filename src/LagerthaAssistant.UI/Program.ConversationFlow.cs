using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Models.AI;

namespace LagerthaAssistant.UI;

internal static partial class Program
{
    private static bool IsCommandResult(ConversationAgentResult result)
    {
        return result.AgentName.Equals("command-agent", StringComparison.OrdinalIgnoreCase);
    }

    private static void PrintCommandResult(ConversationAgentResult result)
    {
        var message = string.IsNullOrWhiteSpace(result.Message)
            ? $"Command '{result.Intent}' executed."
            : result.Message;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Assistant > {message}");
        Console.ResetColor();
    }

    private static async Task<VocabularySaveMode> HandleVocabularyAgentResultAsync(
        ConversationAgentResult result,
        IVocabularyDeckService vocabularyDeckService,
        IVocabularyPersistenceService vocabularyPersistenceService,
        IVocabularyWorkflowService vocabularyWorkflowService,
        VocabularySaveMode saveMode)
    {
        if (result.Items.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("warning: Could not process vocabulary input.");
            Console.ResetColor();
            Console.WriteLine();
            return saveMode;
        }

        if (!result.IsBatch || result.Items.Count == 1)
        {
            return await HandleVocabularyAgentItemAsync(
                result.Items[0],
                vocabularyDeckService,
                vocabularyPersistenceService,
                vocabularyWorkflowService,
                saveMode);
        }

        return await HandleVocabularyBatchAgentItemsAsync(
            result.Items,
            vocabularyDeckService,
            vocabularyPersistenceService,
            saveMode);
    }

    private static async Task<VocabularySaveMode> HandleVocabularyAgentItemAsync(
        ConversationAgentItemResult item,
        IVocabularyDeckService vocabularyDeckService,
        IVocabularyPersistenceService vocabularyPersistenceService,
        IVocabularyWorkflowService vocabularyWorkflowService,
        VocabularySaveMode saveMode)
    {
        if (item.IsWordUnrecognized)
        {
            var correctedWord = AskWordCorrection(item.Input, item.WordSuggestions);
            if (correctedWord is not null)
            {
                var correctedResult = await vocabularyWorkflowService.ProcessAsync(
                    correctedWord, bypassValidation: true);
                var mappedItem = new ConversationAgentItemResult(
                    correctedResult.Input,
                    correctedResult.Lookup,
                    correctedResult.AssistantCompletion,
                    correctedResult.AppendPreview);
                return await HandleVocabularyAgentItemAsync(
                    mappedItem,
                    vocabularyDeckService,
                    vocabularyPersistenceService,
                    vocabularyWorkflowService,
                    saveMode);
            }

            Console.WriteLine();
            return saveMode;
        }

        if (item.FoundInDeck)
        {
            PrintVocabularyFromDeck(item.Lookup);
            Console.WriteLine();
            return saveMode;
        }

        if (item.AssistantCompletion is null || item.AppendPreview is null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("warning: Could not process vocabulary input.");
            Console.ResetColor();
            Console.WriteLine();
            return saveMode;
        }

        var completion = item.AssistantCompletion;
        var preview = item.AppendPreview;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Assistant ({completion.Model}) > {completion.Content}");
        Console.ResetColor();

        if (preview.Status == VocabularyAppendPreviewStatus.ReadyToAppend
            && !string.IsNullOrWhiteSpace(preview.TargetDeckFileName)
            && !string.IsNullOrWhiteSpace(preview.TargetDeckPath))
        {
            var shouldSave = saveMode != VocabularySaveMode.Off;
            var targetDeckFileName = preview.TargetDeckFileName;
            string? overridePartOfSpeech = null;
            var retryPreview = preview;

            if (saveMode == VocabularySaveMode.Ask)
            {
                var confirmationChoice = AskVocabularySaveConfirmation(preview);
                shouldSave = confirmationChoice != SaveConfirmationChoice.No;

                if (confirmationChoice == SaveConfirmationChoice.YesDontAskAgain)
                {
                    saveMode = VocabularySaveMode.Auto;
                }
                else if (confirmationChoice == SaveConfirmationChoice.SaveToOtherDeck)
                {
                    var customSave = await AskCustomSaveTargetAsync(vocabularyDeckService, preview);
                    shouldSave = customSave.ShouldSave;

                    if (customSave.ShouldSave)
                    {
                        targetDeckFileName = customSave.DeckFileName;
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
                var appendResult = await vocabularyPersistenceService.AppendFromAssistantReplyAsync(
                    item.Input,
                    completion.Content,
                    targetDeckFileName,
                    overridePartOfSpeech);
                PrintVocabularyAppendResult(appendResult);

                while (IsFileLockedSaveError(appendResult) && AskRetrySaveConfirmation(retryPreview))
                {
                    appendResult = await vocabularyPersistenceService.AppendFromAssistantReplyAsync(
                        item.Input,
                        completion.Content,
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

        if (completion.Usage is not null)
        {
            Console.WriteLine(
                $"Tokens: prompt={completion.Usage.PromptTokens}, completion={completion.Usage.CompletionTokens}, total={completion.Usage.TotalTokens}");
        }

        Console.WriteLine();
        return saveMode;
    }

    private static async Task<VocabularySaveMode> HandleVocabularyBatchAgentItemsAsync(
        IReadOnlyList<ConversationAgentItemResult> items,
        IVocabularyDeckService vocabularyDeckService,
        IVocabularyPersistenceService vocabularyPersistenceService,
        VocabularySaveMode saveMode)
    {
        var pendingSaves = new List<PendingVocabularySave>();

        foreach (var item in items)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Processing: {item.Input}");
            Console.ResetColor();

            if (item.FoundInDeck)
            {
                PrintVocabularyFromDeck(item.Lookup);
                continue;
            }

            if (item.AssistantCompletion is null || item.AppendPreview is null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("warning: Could not process vocabulary input.");
                Console.ResetColor();
                continue;
            }

            var completion = item.AssistantCompletion;
            var preview = item.AppendPreview;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Assistant ({completion.Model}) > {completion.Content}");
            Console.ResetColor();

            if (preview.Status == VocabularyAppendPreviewStatus.ReadyToAppend
                && !string.IsNullOrWhiteSpace(preview.TargetDeckFileName)
                && !string.IsNullOrWhiteSpace(preview.TargetDeckPath))
            {
                pendingSaves.Add(new PendingVocabularySave(item.Input, completion.Content, preview));
            }
            else
            {
                PrintVocabularyAppendPreviewResult(preview);
            }

            if (completion.Usage is not null)
            {
                Console.WriteLine(
                    $"Tokens: prompt={completion.Usage.PromptTokens}, completion={completion.Usage.CompletionTokens}, total={completion.Usage.TotalTokens}");
            }
        }

        return await FinalizePendingBatchSavesAsync(
            pendingSaves,
            vocabularyDeckService,
            vocabularyPersistenceService,
            saveMode);
    }
    private static async Task<VocabularySaveMode> ProcessBatchInputsAsync(
        IReadOnlyList<string> batchItems,
        IVocabularyWorkflowService vocabularyWorkflowService,
        IVocabularyDeckService vocabularyDeckService,
        IVocabularyPersistenceService vocabularyPersistenceService,
        VocabularySaveMode saveMode)
    {
        var workflowItems = await vocabularyWorkflowService.ProcessBatchAsync(batchItems);
        var mappedItems = workflowItems
            .Select(item => new ConversationAgentItemResult(
                item.Input,
                item.Lookup,
                item.AssistantCompletion,
                item.AppendPreview))
            .ToList();

        return await HandleVocabularyBatchAgentItemsAsync(
            mappedItems,
            vocabularyDeckService,
            vocabularyPersistenceService,
            saveMode);
    }

    private static async Task<VocabularySaveMode> FinalizePendingBatchSavesAsync(
        IReadOnlyList<PendingVocabularySave> pendingSaves,
        IVocabularyDeckService vocabularyDeckService,
        IVocabularyPersistenceService vocabularyPersistenceService,
        VocabularySaveMode saveMode)
    {
        if (pendingSaves.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("info: No new cards are pending save in this batch.");
            Console.ResetColor();
            Console.WriteLine();
            return saveMode;
        }

        if (saveMode == VocabularySaveMode.Off)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("info: Save skipped for batch (mode=off).");
            Console.ResetColor();
            Console.WriteLine();
            return saveMode;
        }

        IReadOnlyList<PendingVocabularySave> saveQueue = pendingSaves;
        var shouldSave = true;

        if (saveMode == VocabularySaveMode.Ask)
        {
            var confirmation = AskBatchSaveConfirmation(pendingSaves);

            switch (confirmation)
            {
                case BatchSaveConfirmationChoice.SaveAll:
                    saveQueue = pendingSaves;
                    break;
                case BatchSaveConfirmationChoice.SaveAllDontAskAgain:
                    saveQueue = pendingSaves;
                    saveMode = VocabularySaveMode.Auto;
                    break;
                case BatchSaveConfirmationChoice.ReviewTargets:
                    saveQueue = await ReviewBatchSaveTargetsAsync(pendingSaves, vocabularyDeckService);
                    shouldSave = saveQueue.Count > 0;
                    break;
                case BatchSaveConfirmationChoice.SkipAll:
                    shouldSave = false;
                    break;
            }
        }

        if (!shouldSave)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("info: Save skipped (declined by user).");
            Console.ResetColor();
            Console.WriteLine();
            return saveMode;
        }

        await SavePendingBatchItemsAsync(saveQueue, vocabularyPersistenceService);
        Console.WriteLine();

        return saveMode;
    }

    private static async Task SavePendingBatchItemsAsync(
        IReadOnlyList<PendingVocabularySave> pendingSaves,
        IVocabularyPersistenceService vocabularyPersistenceService)
    {
        foreach (var pending in pendingSaves)
        {
            if (string.IsNullOrWhiteSpace(pending.TargetDeckFileName))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"warning: Missing target deck for '{pending.RequestedWord}'. Skipped.");
                Console.ResetColor();
                continue;
            }

            var appendResult = await vocabularyPersistenceService.AppendFromAssistantReplyAsync(
                pending.RequestedWord,
                pending.AssistantReply,
                pending.TargetDeckFileName,
                pending.OverridePartOfSpeech);

            PrintVocabularyAppendResult(appendResult);

            var retryPreview = pending.Preview with
            {
                TargetDeckFileName = pending.TargetDeckFileName,
                TargetDeckPath = pending.TargetDeckPath
            };

            while (IsFileLockedSaveError(appendResult) && AskRetrySaveConfirmation(retryPreview))
            {
                appendResult = await vocabularyPersistenceService.AppendFromAssistantReplyAsync(
                    pending.RequestedWord,
                    pending.AssistantReply,
                    pending.TargetDeckFileName,
                    pending.OverridePartOfSpeech);

                PrintVocabularyAppendResult(appendResult);
            }
        }
    }

    private static async Task<IReadOnlyList<PendingVocabularySave>> ReviewBatchSaveTargetsAsync(
        IReadOnlyList<PendingVocabularySave> pendingSaves,
        IVocabularyDeckService vocabularyDeckService)
    {
        var selected = new List<PendingVocabularySave>();

        for (var index = 0; index < pendingSaves.Count; index++)
        {
            var pending = pendingSaves[index];
            var choice = AskBatchItemSaveChoice(pending, index + 1, pendingSaves.Count);

            if (choice == BatchItemSaveChoice.Skip)
            {
                continue;
            }

            if (choice == BatchItemSaveChoice.SaveSuggested)
            {
                selected.Add(pending);
                continue;
            }

            var custom = await AskCustomSaveTargetAsync(vocabularyDeckService, pending.Preview);
            if (!custom.ShouldSave
                || string.IsNullOrWhiteSpace(custom.DeckFileName))
            {
                continue;
            }

            pending.TargetDeckFileName = custom.DeckFileName;
            pending.TargetDeckPath = custom.DeckPath;
            pending.OverridePartOfSpeech = custom.OverridePartOfSpeech;

            selected.Add(pending);
        }

        return selected;
    }

    private static BatchSaveConfirmationChoice AskBatchSaveConfirmation(IReadOnlyList<PendingVocabularySave> pendingSaves)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Batch completed. {pendingSaves.Count} card(s) are ready to save.");
        Console.ResetColor();
        PrintBatchSavePreview(pendingSaves);

        Console.WriteLine("1) Save all suggested targets");
        Console.WriteLine("2) Save all and don't ask again in this session");
        Console.WriteLine("3) Review target deck for each card");
        Console.WriteLine("4) Do not save");

        while (true)
        {
            Console.Write("Select [1/2/3/4]: ");
            if (!TryReadTrimmedLowerInput(out var answer))
            {
                return BatchSaveConfirmationChoice.SkipAll;
            }

            if (answer is "1" or "yes" or "y")
            {
                return BatchSaveConfirmationChoice.SaveAll;
            }

            if (answer is "2" or "always" or "a")
            {
                return BatchSaveConfirmationChoice.SaveAllDontAskAgain;
            }

            if (answer is "3" or "review" or "r")
            {
                return BatchSaveConfirmationChoice.ReviewTargets;
            }

            if (answer is "4" or "no" or "n")
            {
                return BatchSaveConfirmationChoice.SkipAll;
            }

            Console.WriteLine("Please enter 1, 2, 3, or 4.");
        }
    }


    private static void PrintBatchSavePreview(IReadOnlyList<PendingVocabularySave> pendingSaves)
    {
        const int itemColumnWidth = 36;
        const int deckColumnWidth = 36;
        const int markerColumnWidth = 8;

        const string headerIndex = "#";
        const string headerItem = "item";
        const string headerDeck = "deck";
        const string headerMarker = "marker";

        Console.WriteLine();
        Console.WriteLine("Pending save targets:");
        Console.WriteLine($"{headerIndex,2} | {headerItem,-itemColumnWidth} | {headerDeck,-deckColumnWidth} | {headerMarker,-markerColumnWidth}");

        for (var index = 0; index < pendingSaves.Count; index++)
        {
            var pending = pendingSaves[index];
            var marker = GetBatchSuggestedPosMarker(pending);
            var markerValue = $"({marker})";

            Console.WriteLine(
                $"{index + 1,2} | {TruncateForDisplay(pending.RequestedWord, itemColumnWidth),-itemColumnWidth} | {TruncateForDisplay(pending.TargetDeckFileName, deckColumnWidth),-deckColumnWidth} | {markerValue,-markerColumnWidth}");
        }

        Console.WriteLine();
    }

    private static string GetBatchSuggestedPosMarker(PendingVocabularySave pending)
    {
        if (!string.IsNullOrWhiteSpace(pending.OverridePartOfSpeech)
            && TryNormalizePartOfSpeechMarker(pending.OverridePartOfSpeech, out var normalizedOverride))
        {
            return normalizedOverride;
        }

        if (TryExtractPartOfSpeechMarker(pending.AssistantReply, out var markerFromReply))
        {
            return markerFromReply;
        }

        var markerFromDeck = GetSuggestedPosMarkerForDeckFileName(pending.TargetDeckFileName);
        return string.IsNullOrWhiteSpace(markerFromDeck)
            ? "n/a"
            : markerFromDeck;
    }

    private static bool TryExtractPartOfSpeechMarker(string assistantReply, out string marker)
    {
        marker = string.Empty;

        if (string.IsNullOrWhiteSpace(assistantReply))
        {
            return false;
        }

        var lines = assistantReply
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            if (!line.StartsWith("(", StringComparison.Ordinal))
            {
                continue;
            }

            var closeIndex = line.IndexOf(')');
            if (closeIndex <= 1)
            {
                continue;
            }

            var candidate = line[1..closeIndex].Trim();
            if (TryNormalizePartOfSpeechMarker(candidate, out var normalized))
            {
                marker = normalized;
                return true;
            }
        }

        return false;
    }

    private static string TruncateForDisplay(string value, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        if (maxLength <= 3)
        {
            return normalized[..maxLength];
        }

        return normalized[..(maxLength - 3)] + "...";
    }
    private static BatchItemSaveChoice AskBatchItemSaveChoice(PendingVocabularySave pending, int index, int total)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[{index}/{total}] {pending.RequestedWord}");
        Console.ResetColor();
        Console.WriteLine($"Suggested deck: {pending.TargetDeckFileName}");
        if (!string.IsNullOrWhiteSpace(pending.TargetDeckPath))
        {
            Console.WriteLine($"Target path: {pending.TargetDeckPath}");
        }

        Console.WriteLine("1) Save suggested target");
        Console.WriteLine("2) Save to another deck with custom POS marker");
        Console.WriteLine("3) Skip this card");

        while (true)
        {
            Console.Write("Select [1/2/3]: ");
            if (!TryReadTrimmedLowerInput(out var answer))
            {
                return BatchItemSaveChoice.Skip;
            }

            if (answer is "1" or "yes" or "y")
            {
                return BatchItemSaveChoice.SaveSuggested;
            }

            if (answer is "2" or "custom" or "c")
            {
                return BatchItemSaveChoice.SaveToOtherDeck;
            }

            if (answer is "3" or "skip" or "s" or "no" or "n")
            {
                return BatchItemSaveChoice.Skip;
            }

            Console.WriteLine("Please enter 1, 2, or 3.");
        }
    }

    private static string? AskWordCorrection(string input, IReadOnlyList<string> suggestions)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Word '{input}' not recognized.");
        Console.ResetColor();

        if (suggestions.Count > 0)
        {
            Console.WriteLine("Did you mean:");
            for (var i = 0; i < suggestions.Count; i++)
            {
                Console.WriteLine($"  {i + 1}) {suggestions[i]}");
            }
            Console.WriteLine($"  {suggestions.Count + 1}) Enter word manually");
            Console.WriteLine($"  {suggestions.Count + 2}) Skip");

            while (true)
            {
                Console.Write($"Select [1-{suggestions.Count + 2}]: ");
                if (!TryReadTrimmedLowerInput(out var answer))
                    return null;

                if (int.TryParse(answer, out var choice))
                {
                    if (choice >= 1 && choice <= suggestions.Count)
                        return suggestions[choice - 1];

                    if (choice == suggestions.Count + 1)
                        break;

                    if (choice == suggestions.Count + 2)
                        return null;
                }

                Console.WriteLine($"Please enter a number between 1 and {suggestions.Count + 2}.");
            }
        }

        Console.Write("Enter word: ");
        if (!TryReadTrimmedLowerInput(out var manualWord) || string.IsNullOrWhiteSpace(manualWord))
            return null;

        return manualWord;
    }

}

