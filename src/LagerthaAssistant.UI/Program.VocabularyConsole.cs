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
            if (!TryReadInputLine(out var line))
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
            if (!TryReadTrimmedLowerInput(out var answer))
            {
                return false;
            }

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
            if (!TryReadTrimmedLowerInput(out var answer))
            {
                return SaveConfirmationChoice.No;
            }

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
            if (!TryReadTrimmedInput(out var answer))
            {
                return null;
            }

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
        var options = VocabularyPartOfSpeechCatalog.GetOptions();

        Console.WriteLine("Choose POS marker:");
        foreach (var option in options)
        {
            WritePosMarkerOption(option, suggestedMarker);
        }

        Console.WriteLine("0) Cancel");
        var availableNumbers = string.Join('/', options.Select(option => option.Number.ToString(CultureInfo.InvariantCulture)).Append("0"));
        var supportedMarkers = string.Join(", ", options.Select(option => option.Marker));

        while (true)
        {
            Console.Write($"Select [{availableNumbers}] or type marker: ");
            if (!TryReadTrimmedInput(out var answer))
            {
                return null;
            }

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

            Console.WriteLine($"Unsupported marker. Use 1..{options.Count} or one of: {supportedMarkers}.");
        }
    }

    private static void WritePosMarkerOption(
        VocabularyPartOfSpeechOption option,
        string? suggestedMarker)
    {
        var isSuggested = !string.IsNullOrWhiteSpace(suggestedMarker)
            && option.Marker.Equals(suggestedMarker, StringComparison.OrdinalIgnoreCase);

        var optionText = isSuggested
            ? $"{option.Number}) {option.Marker} ({option.Label}) (suggested)"
            : $"{option.Number}) {option.Marker} ({option.Label})";

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
        return VocabularyDeckMarkerSuggester.SuggestMarker(deckFileName);
    }

    private static bool AskYesNo(string question)
    {
        Console.WriteLine(question);
        Console.WriteLine("1) Yes");
        Console.WriteLine("2) No");

        while (true)
        {
            Console.Write("Select [1/2]: ");
            if (!TryReadTrimmedLowerInput(out var answer))
            {
                return false;
            }

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
        return VocabularyPartOfSpeechCatalog.TryNormalize(value, out marker);
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
