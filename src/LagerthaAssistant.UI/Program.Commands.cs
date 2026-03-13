using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.UI.Constants;

namespace LagerthaAssistant.UI;

internal static partial class Program
{
    private readonly record struct CommandHandlingResult(bool Handled, bool ShouldExit, SaveMode SaveMode)
    {
        public static CommandHandlingResult NotHandled(SaveMode saveMode) => new(false, false, saveMode);

        public static CommandHandlingResult Continue(SaveMode saveMode) => new(true, false, saveMode);

        public static CommandHandlingResult Exit(SaveMode saveMode) => new(true, true, saveMode);
    }

    private static async Task<CommandHandlingResult> TryHandleCommandAsync(
        string command,
        SaveMode saveMode,
        ConversationScope uiScope,
        IConversationOrchestrator conversationOrchestrator,
        IVocabularyWorkflowService vocabularyWorkflowService,
        IVocabularyBatchInputService vocabularyBatchInputService,
        IVocabularyDeckService vocabularyDeckService,
        IVocabularyPersistenceService vocabularyPersistenceService,
        IVocabularyStorageModeProvider vocabularyStorageModeProvider,
        IVocabularyStoragePreferenceService vocabularyStoragePreferenceService,
        IGraphAuthService graphAuthService,
        IUserMemoryRepository userMemoryRepository,
        IUnitOfWork unitOfWork)
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
            PrintCurrentSaveMode(saveMode);
            return CommandHandlingResult.Continue(saveMode);
        }

        if (command.Equals(ConsoleCommands.SaveMode, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Usage: /save mode ask|auto|off");
            PrintCurrentSaveMode(saveMode);
            return CommandHandlingResult.Continue(saveMode);
        }

        if (command.StartsWith(ConsoleCommands.SaveMode + " ", StringComparison.OrdinalIgnoreCase))
        {
            var modeText = command[ConsoleCommands.SaveMode.Length..].Trim();
            if (!TryParseSaveMode(modeText, out var updatedSaveMode))
            {
                Console.WriteLine("Usage: /save mode ask|auto|off");
                return CommandHandlingResult.Continue(saveMode);
            }

            saveMode = updatedSaveMode;
            await PersistSaveModeAsync(userMemoryRepository, unitOfWork, saveMode, uiScope);
            PrintCurrentSaveMode(saveMode);
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

            await PersistStorageModeAsync(vocabularyStoragePreferenceService, updatedStorageMode, vocabularyStorageModeProvider, uiScope);
            PrintCurrentStorageMode(vocabularyStorageModeProvider, updatedStorageMode);

            if (updatedStorageMode == VocabularyStorageMode.Graph)
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
                    await PersistStorageModeAsync(
                        vocabularyStoragePreferenceService,
                        VocabularyStorageMode.Graph,
                        vocabularyStorageModeProvider,
                        uiScope);
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

