using System.Globalization;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;

namespace LagerthaAssistant.UI;

internal static partial class Program
{
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
}
