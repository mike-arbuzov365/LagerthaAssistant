using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;
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
        IAssistantSessionService assistantSession,
        IVocabularyWorkflowService vocabularyWorkflowService,
        IVocabularyDeckService vocabularyDeckService,
        IVocabularyPersistenceService vocabularyPersistenceService,
        IVocabularyStorageModeProvider vocabularyStorageModeProvider,
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
            await PersistSaveModeAsync(userMemoryRepository, unitOfWork, saveMode);
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

            vocabularyStorageModeProvider.SetMode(updatedStorageMode);
            await PersistStorageModeAsync(userMemoryRepository, unitOfWork, updatedStorageMode, vocabularyStorageModeProvider);
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

        var setPrefix = $"{ConsoleCommands.Prompt} set";
        if (command.Equals(setPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var capturedPrompt = ReadMultilinePrompt();
            if (string.IsNullOrWhiteSpace(capturedPrompt))
            {
                Console.WriteLine("Prompt update cancelled.");
                return CommandHandlingResult.Continue(saveMode);
            }

            var updated = await assistantSession.SetSystemPromptAsync(capturedPrompt, "manual");
            Console.WriteLine("System prompt updated and saved.");
            PrintSystemPrompt(updated);
            return CommandHandlingResult.Continue(saveMode);
        }

        return CommandHandlingResult.NotHandled(saveMode);
    }
}
