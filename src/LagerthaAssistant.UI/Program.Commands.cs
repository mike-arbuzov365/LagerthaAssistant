using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.UI.Constants;

namespace LagerthaAssistant.UI;

internal static partial class Program
{
    private readonly record struct CommandHandlingResult(bool Handled, bool ShouldExit, VocabularySaveMode SaveMode)
    {
        public static CommandHandlingResult NotHandled(VocabularySaveMode saveMode) => new(false, false, saveMode);

        public static CommandHandlingResult Continue(VocabularySaveMode saveMode) => new(true, false, saveMode);

        public static CommandHandlingResult Exit(VocabularySaveMode saveMode) => new(true, true, saveMode);
    }

    private static async Task<CommandHandlingResult> TryHandleCommandAsync(
        string command,
        VocabularySaveMode saveMode,
        ConversationScope uiScope,
        IConversationOrchestrator conversationOrchestrator,
        IVocabularyWorkflowService vocabularyWorkflowService,
        IVocabularyBatchInputService vocabularyBatchInputService,
        IVocabularyDeckService vocabularyDeckService,
        IVocabularyPersistenceService vocabularyPersistenceService,
        IVocabularySaveModePreferenceService vocabularySaveModePreferenceService,
        IVocabularySessionPreferenceService vocabularySessionPreferenceService,
        IVocabularyStorageModeProvider vocabularyStorageModeProvider,
        IGraphAuthService graphAuthService)
    {
        if (command.Equals(ConsoleCommands.Help, StringComparison.OrdinalIgnoreCase))
        {
            PrintHelp();
            return CommandHandlingResult.Continue(saveMode);
        }

        if (command.Equals(ConsoleCommands.Batch, StringComparison.OrdinalIgnoreCase))
        {
            var rawBatchInput = ReadBatchInput();
            if (rawBatchInput is null)
            {
                Console.WriteLine("Batch input cancelled.");
                return CommandHandlingResult.Continue(saveMode);
            }

            var batchParse = vocabularyBatchInputService.Parse(rawBatchInput);
            var parsedItems = batchParse.Items;

            if (batchParse.ShouldOfferSpaceSplit
                && batchParse.SpaceSplitCandidates.Count > 1
                && !string.IsNullOrWhiteSpace(batchParse.SingleItemWithoutSeparators)
                && AskBatchSpaceSplitChoice(batchParse.SingleItemWithoutSeparators, batchParse.SpaceSplitCandidates))
            {
                parsedItems = batchParse.SpaceSplitCandidates;
            }

            if (parsedItems.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("warning: No valid items detected in batch input.");
                Console.ResetColor();
                return CommandHandlingResult.Continue(saveMode);
            }

            PrintBatchItems(parsedItems);
            if (!AskYesNo("Process these items now?"))
            {
                Console.WriteLine("Batch processing cancelled.");
                return CommandHandlingResult.Continue(saveMode);
            }

            saveMode = await ProcessBatchInputsAsync(
                parsedItems,
                vocabularyWorkflowService,
                vocabularyDeckService,
                vocabularyPersistenceService,
                saveMode);

            return CommandHandlingResult.Continue(saveMode);
        }

        if (command.Equals(ConsoleCommands.Save, StringComparison.OrdinalIgnoreCase))
        {
            PrintCurrentSaveMode(vocabularySaveModePreferenceService, saveMode);
            return CommandHandlingResult.Continue(saveMode);
        }

        if (command.Equals(ConsoleCommands.SaveMode, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Usage: /save mode ask|auto|off");
            PrintCurrentSaveMode(vocabularySaveModePreferenceService, saveMode);
            return CommandHandlingResult.Continue(saveMode);
        }

        if (command.StartsWith(ConsoleCommands.SaveMode + " ", StringComparison.OrdinalIgnoreCase))
        {
            var modeText = command[ConsoleCommands.SaveMode.Length..].Trim();
            if (!vocabularySaveModePreferenceService.TryParse(modeText, out var updatedSaveMode))
            {
                Console.WriteLine("Usage: /save mode ask|auto|off");
                return CommandHandlingResult.Continue(saveMode);
            }

            var session = await vocabularySessionPreferenceService.SetAsync(
                uiScope,
                saveMode: updatedSaveMode);
            saveMode = session.SaveMode;
            PrintCurrentSaveMode(vocabularySaveModePreferenceService, session.SaveMode);
            return CommandHandlingResult.Continue(saveMode);
        }

        if (command.Equals(ConsoleCommands.Storage, StringComparison.OrdinalIgnoreCase))
        {
            PrintCurrentStorageMode(vocabularyStorageModeProvider, vocabularyStorageModeProvider.CurrentMode);
            return CommandHandlingResult.Continue(saveMode);
        }

        if (command.Equals(ConsoleCommands.StorageMode, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Usage: /storage mode local|graph");
            PrintCurrentStorageMode(vocabularyStorageModeProvider, vocabularyStorageModeProvider.CurrentMode);
            return CommandHandlingResult.Continue(saveMode);
        }

        if (command.StartsWith(ConsoleCommands.StorageMode + " ", StringComparison.OrdinalIgnoreCase))
        {
            var modeText = command[ConsoleCommands.StorageMode.Length..].Trim();
            if (!vocabularyStorageModeProvider.TryParse(modeText, out var updatedStorageMode))
            {
                Console.WriteLine("Usage: /storage mode local|graph");
                return CommandHandlingResult.Continue(saveMode);
            }

            var session = await vocabularySessionPreferenceService.SetAsync(
                uiScope,
                storageMode: updatedStorageMode);
            saveMode = session.SaveMode;
            PrintCurrentStorageMode(vocabularyStorageModeProvider, session.StorageMode);

            if (session.StorageMode == VocabularyStorageMode.Graph)
            {
                var status = await graphAuthService.GetStatusAsync();
                PrintGraphStatus(status);
            }

            return CommandHandlingResult.Continue(saveMode);
        }

        if (command.Equals(ConsoleCommands.GraphStatus, StringComparison.OrdinalIgnoreCase))
        {
            var status = await graphAuthService.GetStatusAsync();
            PrintGraphStatus(status);
            return CommandHandlingResult.Continue(saveMode);
        }

        if (command.Equals(ConsoleCommands.GraphLogin, StringComparison.OrdinalIgnoreCase))
        {
            var login = await graphAuthService.LoginAsync(PrintGraphDeviceCodePromptAsync);
            Console.WriteLine(login.Message);

            if (login.Succeeded)
            {
                if (vocabularyStorageModeProvider.CurrentMode != VocabularyStorageMode.Graph
                    && AskYesNo("Switch storage mode to graph now?"))
                {
                    var session = await vocabularySessionPreferenceService.SetAsync(
                        uiScope,
                        storageMode: VocabularyStorageMode.Graph);
                    saveMode = session.SaveMode;
                    PrintCurrentStorageMode(vocabularyStorageModeProvider, VocabularyStorageMode.Graph);
                }

                var status = await graphAuthService.GetStatusAsync();
                PrintGraphStatus(status);
            }

            return CommandHandlingResult.Continue(saveMode);
        }

        if (command.Equals(ConsoleCommands.GraphLogout, StringComparison.OrdinalIgnoreCase))
        {
            await graphAuthService.LogoutAsync();
            Console.WriteLine("Graph token cache cleared.");
            var status = await graphAuthService.GetStatusAsync();
            PrintGraphStatus(status);
            return CommandHandlingResult.Continue(saveMode);
        }

        if (command.Equals(ConsoleCommands.Exit, StringComparison.OrdinalIgnoreCase))
        {
            return CommandHandlingResult.Exit(saveMode);
        }

        var setPrefix = ConsoleCommands.PromptSet;
        if (command.Equals(setPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var capturedPrompt = ReadMultilinePrompt();
            if (string.IsNullOrWhiteSpace(capturedPrompt))
            {
                Console.WriteLine("Prompt update cancelled.");
                return CommandHandlingResult.Continue(saveMode);
            }

            var commandResult = await conversationOrchestrator.ProcessAsync(
                $"{setPrefix} {capturedPrompt}",
                uiScope.Channel,
                uiScope.UserId,
                uiScope.ConversationId);
            if (IsCommandResult(commandResult))
            {
                PrintCommandResult(commandResult);
                Console.WriteLine();
                return CommandHandlingResult.Continue(saveMode);
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("warning: Prompt update was not handled as a command.");
            Console.ResetColor();
            return CommandHandlingResult.Continue(saveMode);
        }

        return CommandHandlingResult.NotHandled(saveMode);
    }
}
