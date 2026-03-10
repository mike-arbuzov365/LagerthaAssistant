namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Infrastructure.Options;

public sealed class VocabularyDeckService : IVocabularyDeckService
{
    private const int HeaderRowNumber = 10;
    private const int DataStartRowNumber = 11;
    private const int HResultSharingViolation = unchecked((int)0x80070020);
    private const int HResultLockViolation = unchecked((int)0x80070021);

    private static readonly XNamespace SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace WorkbookRelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private static readonly char[] WordFormSeparators = ['-', ',', '/', '='];

    private static readonly Dictionary<string, string[]> PosToDeckTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["n"] = ["noun", "nouns"],
        ["v"] = ["verb", "verbs"],
        ["iv"] = ["irregular", "verb", "verbs"],
        ["pv"] = ["phrasal", "phrasalverb", "phrasalverbs", "verb", "verbs"],
        ["adj"] = ["adjective", "adjectives"],
        ["adv"] = ["adverb", "adverbs"],
        ["prep"] = ["preposition", "prepositions"],
        ["conj"] = ["conjunction", "conjunctions"],
        ["pron"] = ["pronoun", "pronouns"]
    };

    private readonly VocabularyDeckOptions _options;
    private readonly IVocabularyReplyParser _replyParser;
    private readonly ILogger<VocabularyDeckService> _logger;

    public VocabularyDeckService(
        VocabularyDeckOptions options,
        IVocabularyReplyParser replyParser,
        ILogger<VocabularyDeckService> logger)
    {
        _options = options;
        _replyParser = replyParser;
        _logger = logger;
    }

    public Task<VocabularyLookupResult> FindInWritableDecksAsync(string word, CancellationToken cancellationToken = default)
    {
        var normalizedWord = NormalizeWord(word);
        if (string.IsNullOrWhiteSpace(normalizedWord))
        {
            return Task.FromResult(new VocabularyLookupResult(string.Empty, []));
        }

        var matches = new List<VocabularyDeckEntry>();
        foreach (var deck in GetWritableDeckFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            matches.AddRange(FindWordInDeck(deck, normalizedWord));
        }

        return Task.FromResult(new VocabularyLookupResult(normalizedWord, matches));
    }

    public Task<IReadOnlyList<VocabularyDeckFile>> GetWritableDeckFilesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var files = GetWritableDeckFiles()
            .Select(deck => new VocabularyDeckFile(deck.FileName, deck.FullPath))
            .ToList();

        return Task.FromResult<IReadOnlyList<VocabularyDeckFile>>(files);
    }

    public Task<VocabularyAppendPreviewResult> PreviewAppendFromAssistantReplyAsync(
        string requestedWord,
        string assistantReply,
        string? forcedDeckFileName = null,
        string? overridePartOfSpeech = null,
        CancellationToken cancellationToken = default)
    {
        var preparation = PrepareAppendFromAssistantReply(
            requestedWord,
            assistantReply,
            forcedDeckFileName,
            overridePartOfSpeech,
            cancellationToken);
        return Task.FromResult(ToPreviewResult(preparation));
    }

    public Task<VocabularyAppendResult> AppendFromAssistantReplyAsync(
        string requestedWord,
        string assistantReply,
        string? forcedDeckFileName = null,
        string? overridePartOfSpeech = null,
        CancellationToken cancellationToken = default)
    {
        var preparation = PrepareAppendFromAssistantReply(
            requestedWord,
            assistantReply,
            forcedDeckFileName,
            overridePartOfSpeech,
            cancellationToken);
        if (preparation.Status != VocabularyAppendPreviewStatus.ReadyToAppend || preparation.SelectedDeck is null)
        {
            return Task.FromResult(ToAppendResult(preparation));
        }

        try
        {
            var rowNumber = AppendRow(preparation.SelectedDeck.FullPath, preparation.TargetWord, preparation.MeaningText, preparation.ExamplesText);
            var addedEntry = new VocabularyDeckEntry(
                preparation.SelectedDeck.FileName,
                preparation.SelectedDeck.FullPath,
                rowNumber,
                preparation.TargetWord,
                preparation.MeaningText,
                preparation.ExamplesText);

            return Task.FromResult(new VocabularyAppendResult(VocabularyAppendStatus.Added, Entry: addedEntry));
        }
        catch (Exception ex)
        {
            if (IsFileLockedException(ex))
            {
                _logger.LogWarning(
                    "Could not append vocabulary word {Word} to deck {DeckFile} because the file is currently in use.",
                    preparation.TargetWord,
                    preparation.SelectedDeck.FileName);

                return Task.FromResult(new VocabularyAppendResult(
                    VocabularyAppendStatus.Error,
                    Message: $"Failed to append vocabulary card to {preparation.SelectedDeck.FileName}: file is open in another app. Close it and try again."));
            }

            _logger.LogError(ex, "Failed to append vocabulary word {Word} to deck {DeckFile}", preparation.TargetWord, preparation.SelectedDeck.FileName);
            return Task.FromResult(new VocabularyAppendResult(
                VocabularyAppendStatus.Error,
                Message: $"Failed to append vocabulary card to {preparation.SelectedDeck.FileName}: {ex.Message}"));
        }
    }

    private AppendPreparation PrepareAppendFromAssistantReply(
        string requestedWord,
        string assistantReply,
        string? forcedDeckFileName,
        string? overridePartOfSpeech,
        CancellationToken cancellationToken)
    {
        if (!_replyParser.TryParse(assistantReply, out var parsedReply) || parsedReply is null)
        {
            return new AppendPreparation(
                VocabularyAppendPreviewStatus.ParseFailed,
                Message: "Assistant response format is invalid, skipped Excel save.");
        }

        var normalizedRequestedWord = NormalizeWord(requestedWord);
        var normalizedParsedWord = NormalizeWord(parsedReply.Word);
        var targetWord = string.IsNullOrWhiteSpace(normalizedParsedWord)
            ? normalizedRequestedWord
            : normalizedParsedWord;

        if (string.IsNullOrWhiteSpace(targetWord))
        {
            return new AppendPreparation(
                VocabularyAppendPreviewStatus.ParseFailed,
                Message: "Word is empty after parsing, skipped Excel save.");
        }

        var writableDecks = GetWritableDeckFiles();
        if (writableDecks.Count == 0)
        {
            return new AppendPreparation(
                VocabularyAppendPreviewStatus.NoWritableDecks,
                targetWord,
                Message: "No writable vocabulary decks found.");
        }

        var duplicates = new List<VocabularyDeckEntry>();
        foreach (var deck in writableDecks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            duplicates.AddRange(FindWordInDeck(deck, targetWord));
        }

        if (duplicates.Count > 0)
        {
            return new AppendPreparation(
                VocabularyAppendPreviewStatus.DuplicateFound,
                targetWord,
                Duplicates: duplicates,
                Message: "Word already exists in writable decks.");
        }

        DeckFile? selectedDeck;
        if (!string.IsNullOrWhiteSpace(forcedDeckFileName))
        {
            selectedDeck = writableDecks.FirstOrDefault(x => x.FileName.Equals(forcedDeckFileName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (selectedDeck is null)
            {
                return new AppendPreparation(
                    VocabularyAppendPreviewStatus.NoMatchingDeck,
                    targetWord,
                    Message: $"Deck '{forcedDeckFileName}' is not a writable deck.");
            }
        }
        else
        {
            selectedDeck = SelectTargetDeck(writableDecks, parsedReply, normalizedRequestedWord, targetWord);
            if (selectedDeck is null)
            {
                return new AppendPreparation(
                    VocabularyAppendPreviewStatus.NoMatchingDeck,
                    targetWord,
                    Message: "Could not choose a writable deck for this word.");
            }
        }

        var normalizedMeanings = NormalizeMeaningsWithOverridePos(parsedReply.Meanings, overridePartOfSpeech);
        var meaningText = string.Join(Environment.NewLine + Environment.NewLine, normalizedMeanings);
        var examplesText = string.Join(Environment.NewLine + Environment.NewLine, parsedReply.Examples);

        return new AppendPreparation(
            VocabularyAppendPreviewStatus.ReadyToAppend,
            targetWord,
            meaningText,
            examplesText,
            selectedDeck);
    }

    private static VocabularyAppendPreviewResult ToPreviewResult(AppendPreparation preparation)
    {
        if (preparation.Status == VocabularyAppendPreviewStatus.ReadyToAppend && preparation.SelectedDeck is not null)
        {
            return new VocabularyAppendPreviewResult(
                VocabularyAppendPreviewStatus.ReadyToAppend,
                preparation.TargetWord,
                preparation.SelectedDeck.FileName,
                preparation.SelectedDeck.FullPath,
                preparation.Duplicates,
                preparation.Message);
        }

        return new VocabularyAppendPreviewResult(
            preparation.Status,
            preparation.TargetWord,
            DuplicateMatches: preparation.Duplicates,
            Message: preparation.Message);
    }

    private static VocabularyAppendResult ToAppendResult(AppendPreparation preparation)
    {
        var status = preparation.Status switch
        {
            VocabularyAppendPreviewStatus.DuplicateFound => VocabularyAppendStatus.DuplicateFound,
            VocabularyAppendPreviewStatus.ParseFailed => VocabularyAppendStatus.ParseFailed,
            VocabularyAppendPreviewStatus.NoWritableDecks => VocabularyAppendStatus.NoWritableDecks,
            VocabularyAppendPreviewStatus.NoMatchingDeck => VocabularyAppendStatus.NoMatchingDeck,
            _ => VocabularyAppendStatus.Error
        };

        return new VocabularyAppendResult(
            status,
            DuplicateMatches: preparation.Duplicates,
            Message: preparation.Message);
    }

    private static IReadOnlyList<string> NormalizeMeaningsWithOverridePos(
        IReadOnlyList<string> meanings,
        string? overridePartOfSpeech)
    {
        var normalizedPos = NormalizePartOfSpeech(overridePartOfSpeech);
        if (string.IsNullOrWhiteSpace(normalizedPos))
        {
            return meanings;
        }

        var adjusted = new List<string>(meanings.Count);

        foreach (var meaning in meanings)
        {
            var trimmed = meaning?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (trimmed.StartsWith("(", StringComparison.Ordinal))
            {
                var closeIndex = trimmed.IndexOf(')');
                if (closeIndex > 0)
                {
                    adjusted.Add($"({normalizedPos}) {trimmed[(closeIndex + 1)..].TrimStart()}");
                }
                else
                {
                    adjusted.Add($"({normalizedPos}) {trimmed.Trim('(', ')', ' ')}");
                }
            }
            else
            {
                adjusted.Add($"({normalizedPos}) {trimmed}");
            }
        }

        return adjusted.Count > 0
            ? adjusted
            : meanings;
    }

    private static string? NormalizePartOfSpeech(string? partOfSpeech)
    {
        var value = partOfSpeech?.Trim().ToLowerInvariant();

        return value switch
        {
            "n" or "noun" => "n",
            "v" or "verb" => "v",
            "iv" or "irregular" or "irregular-verb" => "iv",
            "pv" or "phrasal" or "phrasal-verb" => "pv",
            "adj" or "adjective" => "adj",
            "adv" or "adverb" => "adv",
            "prep" or "preposition" => "prep",
            "conj" or "conjunction" => "conj",
            "pron" or "pronoun" => "pron",
            _ => null
        };
    }
    private DeckFile? SelectTargetDeck(
        IReadOnlyList<DeckFile> writableDecks,
        ParsedVocabularyReply parsedReply,
        string normalizedRequestedWord,
        string targetWord)
    {
        if (IsIrregularVerbCandidate(targetWord, parsedReply.PartsOfSpeech))
        {
            var irregularDeck = writableDecks.FirstOrDefault(x => x.FileName.Equals(_options.IrregularVerbDeckFileName, StringComparison.OrdinalIgnoreCase));
            if (irregularDeck is not null)
            {
                return irregularDeck;
            }
        }

        foreach (var partOfSpeech in parsedReply.PartsOfSpeech)
        {
            var preferredDeckFileName = GetPreferredDeckFileName(partOfSpeech);
            if (string.IsNullOrWhiteSpace(preferredDeckFileName))
            {
                continue;
            }

            var preferredDeck = writableDecks.FirstOrDefault(x => x.FileName.Equals(preferredDeckFileName, StringComparison.OrdinalIgnoreCase));
            if (preferredDeck is not null)
            {
                return preferredDeck;
            }
        }

        var textTokens = TokenizeText($"{normalizedRequestedWord} {targetWord} {string.Join(' ', parsedReply.Meanings)} {string.Join(' ', parsedReply.Examples)}");

        DeckFile? best = null;
        var bestScore = int.MinValue;

        foreach (var deck in writableDecks)
        {
            var score = 0;

            foreach (var pos in parsedReply.PartsOfSpeech)
            {
                if (PosToDeckTokens.TryGetValue(pos, out var posTokens) && posTokens.Any(token => deck.Tokens.Contains(token)))
                {
                    score += 12;
                }
            }

            foreach (var token in textTokens)
            {
                if (deck.Tokens.Contains(token))
                {
                    score += 4;
                }
            }

            if ((targetWord.Contains(' ') || normalizedRequestedWord.Contains(' '))
                && (deck.Tokens.Contains("idioms") || deck.Tokens.Contains("phrasal") || deck.Tokens.Contains("expressions")))
            {
                score += 8;
            }

            if (deck.FileName.Equals(_options.FallbackDeckFileName, StringComparison.OrdinalIgnoreCase))
            {
                score += 2;
            }

            if (deck.Tokens.Contains("training"))
            {
                score -= 3;
            }

            if (score > bestScore)
            {
                best = deck;
                bestScore = score;
            }
        }

        if (best is not null && bestScore > 0)
        {
            return best;
        }

        var fallback = writableDecks.FirstOrDefault(x => x.FileName.Equals(_options.FallbackDeckFileName, StringComparison.OrdinalIgnoreCase));
        if (fallback is not null)
        {
            return fallback;
        }

        return writableDecks
            .OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private string? GetPreferredDeckFileName(string partOfSpeech)
    {
        return partOfSpeech.ToLowerInvariant() switch
        {
            "n" => _options.NounDeckFileName,
            "v" => _options.VerbDeckFileName,
            "iv" => _options.IrregularVerbDeckFileName,
            "pv" => _options.PhrasalVerbDeckFileName,
            "adj" => _options.AdjectiveDeckFileName,
            "adv" => _options.AdverbDeckFileName,
            "prep" => _options.PrepositionDeckFileName,
            "conj" => _options.ConjunctionDeckFileName,
            "pron" => _options.PronounDeckFileName,
            _ => null
        };
    }

    private int AppendRow(string deckPath, string word, string meaning, string examples)
    {
        var tempOutputPath = CreateTemporaryWorkbookPath(deckPath);

        try
        {
            int nextRow;

            using (var sourceArchive = OpenArchiveForSharedRead(deckPath))
            {
                var worksheetPath = ResolveFirstWorksheetPath(sourceArchive);
                var worksheetEntry = sourceArchive.GetEntry(worksheetPath)
                    ?? throw new InvalidOperationException($"Worksheet entry '{worksheetPath}' not found.");

                var worksheetDocument = LoadXmlDocument(worksheetEntry);
                var sheetData = worksheetDocument.Root?.Element(SpreadsheetNs + "sheetData")
                    ?? throw new InvalidOperationException("Worksheet does not contain sheetData.");

                var existingRows = sheetData.Elements(SpreadsheetNs + "row").ToList();
                var maxRowNumber = existingRows.Count == 0
                    ? HeaderRowNumber
                    : existingRows.Max(GetRowNumber);

                nextRow = Math.Max(maxRowNumber, HeaderRowNumber) + 1;

                var styleMap = GetStyleMapForColumns(sheetData, maxRowNumber, ["A", "B", "H"]);

                var newRow = new XElement(SpreadsheetNs + "row", new XAttribute("r", nextRow));
                newRow.Add(CreateInlineStringCell("A", nextRow, meaning, styleMap));
                newRow.Add(CreateInlineStringCell("B", nextRow, word, styleMap));
                newRow.Add(CreateInlineStringCell("H", nextRow, examples, styleMap));

                sheetData.Add(newRow);

                var updatedWorksheetBytes = SerializeXmlDocument(worksheetDocument);
                RewriteArchiveWithReplacedEntry(sourceArchive, tempOutputPath, worksheetPath, updatedWorksheetBytes);
            }

            ReplaceOriginalWorkbook(tempOutputPath, deckPath);
            return nextRow;
        }
        finally
        {
            if (File.Exists(tempOutputPath))
            {
                File.Delete(tempOutputPath);
            }
        }
    }

    private static XElement CreateInlineStringCell(string columnName, int rowNumber, string value, IReadOnlyDictionary<string, string> styleMap)
    {
        var textElement = new XElement(SpreadsheetNs + "t", value ?? string.Empty);
        textElement.SetAttributeValue(XNamespace.Xml + "space", "preserve");

        var cell = new XElement(
            SpreadsheetNs + "c",
            new XAttribute("r", $"{columnName}{rowNumber}"),
            new XAttribute("t", "inlineStr"),
            new XElement(SpreadsheetNs + "is", textElement));

        if (styleMap.TryGetValue(columnName, out var styleIndex) && !string.IsNullOrWhiteSpace(styleIndex))
        {
            cell.SetAttributeValue("s", styleIndex);
        }

        return cell;
    }

    private static IReadOnlyDictionary<string, string> GetStyleMapForColumns(XElement sheetData, int maxRowNumber, IReadOnlyList<string> columns)
    {
        var styles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rowNumber in new[] { maxRowNumber, HeaderRowNumber })
        {
            var row = FindRow(sheetData, rowNumber);
            if (row is null)
            {
                continue;
            }

            foreach (var column in columns)
            {
                if (styles.ContainsKey(column))
                {
                    continue;
                }

                var cell = FindCellByColumn(row, column, rowNumber);
                var style = cell?.Attribute("s")?.Value;
                if (!string.IsNullOrWhiteSpace(style))
                {
                    styles[column] = style;
                }
            }

            if (styles.Count == columns.Count)
            {
                break;
            }
        }

        return styles;
    }

    private IReadOnlyList<VocabularyDeckEntry> FindWordInDeck(DeckFile deck, string normalizedWord)
    {
        try
        {
            using var archive = OpenArchiveForSharedRead(deck.FullPath);
            var sharedStrings = LoadSharedStrings(archive);

            var worksheetPath = ResolveFirstWorksheetPath(archive);
            var worksheetEntry = archive.GetEntry(worksheetPath);
            if (worksheetEntry is null)
            {
                _logger.LogWarning("Worksheet entry '{WorksheetPath}' is missing in {DeckPath}", worksheetPath, deck.FullPath);
                return [];
            }

            var worksheetDocument = LoadXmlDocument(worksheetEntry);
            var sheetData = worksheetDocument.Root?.Element(SpreadsheetNs + "sheetData");
            if (sheetData is null)
            {
                return [];
            }

            var matches = new List<VocabularyDeckEntry>();
            foreach (var row in sheetData.Elements(SpreadsheetNs + "row"))
            {
                var rowNumber = GetRowNumber(row);
                if (rowNumber < DataStartRowNumber)
                {
                    continue;
                }

                var word = GetCellText(row, "B", rowNumber, sharedStrings);
                if (!IsLookupMatch(word, normalizedWord))
                {
                    continue;
                }

                var meaning = GetCellText(row, "A", rowNumber, sharedStrings);
                var examples = GetCellText(row, "H", rowNumber, sharedStrings);

                matches.Add(new VocabularyDeckEntry(
                    deck.FileName,
                    deck.FullPath,
                    rowNumber,
                    word.Trim(),
                    meaning.Trim(),
                    examples.Trim()));
            }

            return matches;
        }
        catch (Exception ex)
        {
            if (IsFileLockedException(ex))
            {
                _logger.LogWarning(
                    "Skipping deck {DeckPath} while searching duplicates because the file is currently in use.",
                    deck.FullPath);
                return [];
            }

            _logger.LogError(ex, "Failed to read deck {DeckPath}", deck.FullPath);
            return [];
        }
    }

    private static ZipArchive OpenArchiveForSharedRead(string path)
    {
        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        try
        {
            return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private IReadOnlyList<DeckFile> GetWritableDeckFiles()
    {
        var folderPath = ExpandFolderPath(_options.FolderPath);
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            _logger.LogWarning("Vocabulary deck folder does not exist: {FolderPath}", folderPath);
            return [];
        }

        var pattern = string.IsNullOrWhiteSpace(_options.FilePattern)
            ? "wm-*.xlsx"
            : _options.FilePattern;

        var readOnlyNames = new HashSet<string>(
            _options.ReadOnlyFileNames ?? [],
            StringComparer.OrdinalIgnoreCase);

        var files = Directory
            .EnumerateFiles(folderPath, pattern, SearchOption.TopDirectoryOnly)
            .Where(path => !Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal))
            .Select(path => new DeckFile(Path.GetFileName(path), path, TokenizeFileName(Path.GetFileName(path))))
            .Where(deck => !IsReadOnlyDeck(deck.FileName, readOnlyNames))
            .OrderBy(deck => deck.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return files;
    }

    private static bool IsReadOnlyDeck(string fileName, ISet<string> readOnlyNames)
    {
        return readOnlyNames.Contains(fileName)
            || fileName.Contains("-all-", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExpandFolderPath(string rawPath)
    {
        var value = rawPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Environment.ExpandEnvironmentVariables(value);
    }

    private static XDocument LoadXmlDocument(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static byte[] SerializeXmlDocument(XDocument document)
    {
        using var memoryStream = new MemoryStream();
        using var writer = XmlWriter.Create(memoryStream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            NewLineHandling = NewLineHandling.None
        });

        document.Save(writer);
        writer.Flush();
        return memoryStream.ToArray();
    }

    private static string CreateTemporaryWorkbookPath(string originalPath)
    {
        var directory = Path.GetDirectoryName(originalPath)
            ?? throw new InvalidOperationException($"Could not resolve directory for {originalPath}");

        var fileName = Path.GetFileName(originalPath);
        return Path.Combine(directory, $"{fileName}.{Guid.NewGuid():N}.tmp");
    }

    private static void RewriteArchiveWithReplacedEntry(
        ZipArchive sourceArchive,
        string outputPath,
        string entryPathToReplace,
        byte[] replacementContent)
    {
        var replaced = false;

        using var destinationArchive = ZipFile.Open(outputPath, ZipArchiveMode.Create);

        foreach (var sourceEntry in sourceArchive.Entries)
        {
            var destinationEntry = destinationArchive.CreateEntry(sourceEntry.FullName, CompressionLevel.Optimal);
            destinationEntry.LastWriteTime = sourceEntry.LastWriteTime;

            using var destinationStream = destinationEntry.Open();
            if (sourceEntry.FullName.Equals(entryPathToReplace, StringComparison.OrdinalIgnoreCase))
            {
                destinationStream.Write(replacementContent, 0, replacementContent.Length);
                replaced = true;
                continue;
            }

            using var sourceStream = sourceEntry.Open();
            sourceStream.CopyTo(destinationStream);
        }

        if (!replaced)
        {
            throw new InvalidOperationException($"Worksheet entry '{entryPathToReplace}' was not found in source archive.");
        }
    }

    private static void ReplaceOriginalWorkbook(string temporaryPath, string targetPath)
    {
        if (File.Exists(targetPath))
        {
            try
            {
                File.Replace(temporaryPath, targetPath, null, true);
                return;
            }
            catch (IOException)
            {
                // Fall back to copy overwrite if replace is not available in the current environment.
            }
            catch (PlatformNotSupportedException)
            {
                // Fall back to copy overwrite if replace is not available in the current environment.
            }
        }

        File.Copy(temporaryPath, targetPath, true);
    }

    private static IReadOnlyList<string> LoadSharedStrings(ZipArchive archive)
    {
        var sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedStringsEntry is null)
        {
            return [];
        }

        var document = LoadXmlDocument(sharedStringsEntry);
        return document.Root?
            .Elements(SpreadsheetNs + "si")
            .Select(ReadSharedStringItem)
            .ToList()
            ?? [];
    }

    private static string ReadSharedStringItem(XElement sharedItem)
    {
        var directText = sharedItem.Element(SpreadsheetNs + "t");
        if (directText is not null)
        {
            return directText.Value;
        }

        return string.Concat(sharedItem
            .Elements(SpreadsheetNs + "r")
            .Select(run => run.Element(SpreadsheetNs + "t")?.Value ?? string.Empty));
    }

    private static string ResolveFirstWorksheetPath(ZipArchive archive)
    {
        var workbookEntry = archive.GetEntry("xl/workbook.xml")
            ?? throw new InvalidOperationException("Workbook entry 'xl/workbook.xml' not found.");

        var workbookDocument = LoadXmlDocument(workbookEntry);
        var firstSheet = workbookDocument.Root?
            .Element(SpreadsheetNs + "sheets")?
            .Elements(SpreadsheetNs + "sheet")
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Workbook does not contain sheets.");

        var relationshipId = firstSheet.Attribute(WorkbookRelNs + "id")?.Value
            ?? throw new InvalidOperationException("Sheet relationship id is missing.");

        var relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels")
            ?? throw new InvalidOperationException("Workbook relationships entry is missing.");

        var relationshipsDocument = LoadXmlDocument(relationshipsEntry);
        var relationship = relationshipsDocument.Root?
            .Elements()
            .FirstOrDefault(x => x.Attribute("Id")?.Value == relationshipId)
            ?? throw new InvalidOperationException($"Relationship '{relationshipId}' was not found.");

        var target = relationship.Attribute("Target")?.Value
            ?? throw new InvalidOperationException($"Relationship '{relationshipId}' target is missing.");

        var normalizedTarget = target.Replace('\\', '/');

        if (normalizedTarget.StartsWith("/", StringComparison.Ordinal))
        {
            normalizedTarget = normalizedTarget.TrimStart('/');
        }

        if (!normalizedTarget.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
        {
            normalizedTarget = $"xl/{normalizedTarget.TrimStart('/')}";
        }

        return normalizedTarget;
    }

    private static int GetRowNumber(XElement row)
    {
        var rowNumberText = row.Attribute("r")?.Value;
        if (int.TryParse(rowNumberText, out var rowNumber) && rowNumber > 0)
        {
            return rowNumber;
        }

        var firstCellReference = row.Elements(SpreadsheetNs + "c")
            .Select(cell => cell.Attribute("r")?.Value)
            .FirstOrDefault(reference => !string.IsNullOrWhiteSpace(reference));

        if (!string.IsNullOrWhiteSpace(firstCellReference)
            && int.TryParse(new string(firstCellReference.SkipWhile(char.IsLetter).ToArray()), out rowNumber)
            && rowNumber > 0)
        {
            return rowNumber;
        }

        return 0;
    }

    private static XElement? FindRow(XElement sheetData, int rowNumber)
    {
        return sheetData
            .Elements(SpreadsheetNs + "row")
            .FirstOrDefault(row => GetRowNumber(row) == rowNumber);
    }

    private static XElement? FindCellByColumn(XElement row, string columnName, int fallbackRowNumber)
    {
        foreach (var cell in row.Elements(SpreadsheetNs + "c"))
        {
            var reference = cell.Attribute("r")?.Value;
            if (string.IsNullOrWhiteSpace(reference))
            {
                continue;
            }

            if (GetColumnName(reference).Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return cell;
            }
        }

        return row.Elements(SpreadsheetNs + "c")
            .FirstOrDefault(cell => (cell.Attribute("r")?.Value ?? string.Empty).Equals($"{columnName}{fallbackRowNumber}", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetColumnName(string cellReference)
    {
        return new string(cellReference
            .TakeWhile(char.IsLetter)
            .ToArray());
    }

    private static string GetCellText(XElement row, string columnName, int rowNumber, IReadOnlyList<string> sharedStrings)
    {
        var cell = FindCellByColumn(row, columnName, rowNumber);
        if (cell is null)
        {
            return string.Empty;
        }

        var cellType = cell.Attribute("t")?.Value;

        if (string.Equals(cellType, "s", StringComparison.OrdinalIgnoreCase))
        {
            var sharedIndexText = cell.Element(SpreadsheetNs + "v")?.Value;
            if (int.TryParse(sharedIndexText, out var sharedIndex)
                && sharedIndex >= 0
                && sharedIndex < sharedStrings.Count)
            {
                return sharedStrings[sharedIndex];
            }

            return string.Empty;
        }

        if (string.Equals(cellType, "inlineStr", StringComparison.OrdinalIgnoreCase))
        {
            return ReadInlineString(cell);
        }

        return cell.Element(SpreadsheetNs + "v")?.Value ?? string.Empty;
    }

    private static string ReadInlineString(XElement cell)
    {
        var inline = cell.Element(SpreadsheetNs + "is");
        if (inline is null)
        {
            return string.Empty;
        }

        var directText = inline.Element(SpreadsheetNs + "t");
        if (directText is not null)
        {
            return directText.Value;
        }

        return string.Concat(inline
            .Elements(SpreadsheetNs + "r")
            .Select(run => run.Element(SpreadsheetNs + "t")?.Value ?? string.Empty));
    }

    private static bool IsLookupMatch(string storedWord, string normalizedLookup)
    {
        var normalizedStoredWord = NormalizeWord(storedWord);
        if (normalizedStoredWord.Equals(normalizedLookup, StringComparison.Ordinal))
        {
            return true;
        }

        var forms = SplitWordForms(storedWord);
        if (forms.Count <= 1)
        {
            return false;
        }

        return forms.Any(form => form.Equals(normalizedLookup, StringComparison.Ordinal));
    }

    private static bool IsIrregularVerbCandidate(string targetWord, IReadOnlyList<string> partsOfSpeech)
    {
        var hasVerbPart = partsOfSpeech.Any(pos => pos.Equals("v", StringComparison.OrdinalIgnoreCase)
            || pos.Equals("iv", StringComparison.OrdinalIgnoreCase));

        if (!hasVerbPart)
        {
            return false;
        }

        var forms = SplitWordForms(targetWord);
        return forms.Count >= 3;
    }

    private static IReadOnlyList<string> SplitWordForms(string rawWord)
    {
        if (string.IsNullOrWhiteSpace(rawWord))
        {
            return [];
        }

        var shouldSplit = rawWord.Contains(" - ", StringComparison.Ordinal)
            || rawWord.Contains(',', StringComparison.Ordinal)
            || rawWord.Count(ch => ch == '-') >= 2;

        if (!shouldSplit)
        {
            return [NormalizeWord(rawWord)];
        }

        return rawWord
            .ToLowerInvariant()
            .Split(WordFormSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string NormalizeWord(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static HashSet<string> TokenizeFileName(string fileName)
    {
        var withoutExtension = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        if (withoutExtension.StartsWith("wm-", StringComparison.Ordinal))
        {
            withoutExtension = withoutExtension[3..];
        }

        var tokens = withoutExtension
            .Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 2)
            .Where(token => token is not ("ua" or "en" or "us" or "ru"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return tokens;
    }

    private static IReadOnlySet<string> TokenizeText(string text)
    {
        return Regex.Matches(text.ToLowerInvariant(), "[\\p{L}]+")
            .Select(match => match.Value)
            .Where(token => token.Length >= 2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsFileLockedException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is IOException ioException
                && (ioException.HResult == HResultSharingViolation || ioException.HResult == HResultLockViolation))
            {
                return true;
            }
        }

        return false;
    }

    private sealed record AppendPreparation(
        VocabularyAppendPreviewStatus Status,
        string TargetWord = "",
        string MeaningText = "",
        string ExamplesText = "",
        DeckFile? SelectedDeck = null,
        IReadOnlyList<VocabularyDeckEntry>? Duplicates = null,
        string? Message = null);
    private sealed record DeckFile(string FileName, string FullPath, HashSet<string> Tokens);
}




