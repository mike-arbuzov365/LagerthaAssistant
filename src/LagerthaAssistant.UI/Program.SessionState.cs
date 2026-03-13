using System.Diagnostics;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Domain.Entities;

namespace LagerthaAssistant.UI;

internal static partial class Program
{
    private static async Task<SaveMode> LoadSaveModeAsync(
        IUserMemoryRepository userMemoryRepository,
        ConversationScope scope)
    {
        var entry = await GetScopedOrLegacyEntryAsync(userMemoryRepository, SaveModeMemoryKey, scope);
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
        SaveMode saveMode,
        ConversationScope scope)
    {
        var modeValue = ToSaveModeText(saveMode);
        var now = DateTimeOffset.UtcNow;

        var entry = await userMemoryRepository.GetByKeyAsync(SaveModeMemoryKey, scope.Channel, scope.UserId);
        if (entry is null)
        {
            await userMemoryRepository.AddAsync(new UserMemoryEntry
            {
                Key = SaveModeMemoryKey,
                Value = modeValue,
                Confidence = 1.0,
                IsActive = false,
                LastSeenAtUtc = now,
                Channel = scope.Channel,
                UserId = scope.UserId
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

    private static async Task<UserMemoryEntry?> GetScopedOrLegacyEntryAsync(
        IUserMemoryRepository userMemoryRepository,
        string key,
        ConversationScope scope)
    {
        var scoped = await userMemoryRepository.GetByKeyAsync(key, scope.Channel, scope.UserId);
        if (scoped is not null)
        {
            return scoped;
        }

        if (scope.Channel.Equals(ConversationScope.DefaultChannel, StringComparison.Ordinal)
            && scope.UserId.Equals(ConversationScope.DefaultUserId, StringComparison.Ordinal))
        {
            return null;
        }

        return await userMemoryRepository.GetByKeyAsync(key);
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
