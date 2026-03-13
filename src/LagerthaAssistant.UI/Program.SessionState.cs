using System.Diagnostics;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;

namespace LagerthaAssistant.UI;

internal static partial class Program
{
    private static async Task<VocabularySaveMode> LoadSaveModeAsync(
        IVocabularySaveModePreferenceService vocabularySaveModePreferenceService,
        ConversationScope scope)
    {
        return await vocabularySaveModePreferenceService.GetModeAsync(scope);
    }

    private static async Task PersistSaveModeAsync(
        IVocabularySaveModePreferenceService vocabularySaveModePreferenceService,
        VocabularySaveMode saveMode,
        ConversationScope scope)
    {
        await vocabularySaveModePreferenceService.SetModeAsync(scope, saveMode);
    }

    private static void PrintCurrentSaveMode(
        IVocabularySaveModePreferenceService vocabularySaveModePreferenceService,
        VocabularySaveMode saveMode)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"info: Save mode is '{vocabularySaveModePreferenceService.ToText(saveMode)}'.");
        Console.ResetColor();
    }

    private static async Task<VocabularyStorageMode> LoadStorageModeAsync(
        IVocabularyStoragePreferenceService vocabularyStoragePreferenceService,
        ConversationScope scope)
    {
        return await vocabularyStoragePreferenceService.GetModeAsync(scope);
    }

    private static async Task PersistStorageModeAsync(
        IVocabularyStoragePreferenceService vocabularyStoragePreferenceService,
        VocabularyStorageMode mode,
        IVocabularyStorageModeProvider vocabularyStorageModeProvider,
        ConversationScope scope)
    {
        await vocabularyStoragePreferenceService.SetModeAsync(scope, mode);
        vocabularyStorageModeProvider.SetMode(mode);
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

    private static Task PrintGraphDeviceCodePromptAsync(
        GraphDeviceCodePrompt prompt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(prompt.UserCode))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"! First copy your one-time code: {prompt.UserCode}");
            Console.ResetColor();

            Console.Write($"Press Enter to open {prompt.VerificationUri} in your browser...");
            if (!TryReadInputLine(out _))
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"warning: Open {prompt.VerificationUri} manually and continue sign-in.");
                Console.ResetColor();
                return Task.CompletedTask;
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Open {prompt.VerificationUri} in a browser to continue Graph sign-in.");
            Console.ResetColor();
        }

        if (TryOpenBrowser(prompt.VerificationUri))
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("info: Browser opened. Complete authentication there, then return to this terminal.");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"warning: Could not open a browser automatically. Open {prompt.VerificationUri} manually.");
            Console.ResetColor();
        }

        return Task.CompletedTask;
    }

    private static bool TryOpenBrowser(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? ReadMultilinePrompt()
    {
        Console.WriteLine("Paste the system prompt below.");
        Console.WriteLine("Type /end on a new line to save, or /cancel to abort.");

        var lines = new List<string>();

        while (true)
        {
            if (!TryReadInputLine(out var line))
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
}
