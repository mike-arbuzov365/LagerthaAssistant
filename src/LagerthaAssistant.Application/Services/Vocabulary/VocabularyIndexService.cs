namespace LagerthaAssistant.Application.Services.Vocabulary;

using System.Text.RegularExpressions;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Domain.Enums;
using Microsoft.Extensions.Logging;

public sealed class VocabularyIndexService : IVocabularyIndexService
{
    private static readonly char[] WordFormSeparators = ['-', ',', '/', '='];
    private static readonly char[] TokenTrimChars = ['"', '\'', '`', '.', '!', '?', ';', ':', '(', ')', '[', ']'];
    private static readonly Regex PosFromMeaningRegex = new("^\\((?<pos>[a-z]+)\\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly char[] DashVariants = ['\u2013', '\u2014', '\u2212'];

    private readonly IVocabularyCardRepository _cardRepository;
    private readonly IVocabularySyncJobRepository _syncJobRepository;
    private readonly IVocabularyReplyParser _replyParser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<VocabularyIndexService> _logger;

    public VocabularyIndexService(
        IVocabularyCardRepository cardRepository,
        IVocabularySyncJobRepository syncJobRepository,
        IVocabularyReplyParser replyParser,
        IUnitOfWork unitOfWork,
        ILogger<VocabularyIndexService> logger)
    {
        _cardRepository = cardRepository;
        _syncJobRepository = syncJobRepository;
        _replyParser = replyParser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<VocabularyLookupResult> FindByInputAsync(string input, CancellationToken cancellationToken = default)
    {
        var tokens = BuildTokens(input);
        if (tokens.Count == 0)
        {
            return new VocabularyLookupResult(input, []);
        }

        var cards = await _cardRepository.FindByAnyTokenAsync(tokens, cancellationToken);
        var matches = BuildMatches(cards, tokens);

        return new VocabularyLookupResult(input, matches);
    }

    public async Task<IReadOnlyDictionary<string, VocabularyLookupResult>> FindByInputsAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        if (inputs is null)
        {
            throw new ArgumentNullException(nameof(inputs));
        }

        var normalizedInputs = inputs
            .Where(input => !string.IsNullOrWhiteSpace(input))
            .Select(input => input.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedInputs.Count == 0)
        {
            return new Dictionary<string, VocabularyLookupResult>(StringComparer.OrdinalIgnoreCase);
        }

        var tokensByInput = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var allTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in normalizedInputs)
        {
            var inputTokens = BuildTokens(input);
            tokensByInput[input] = inputTokens;

            foreach (var token in inputTokens)
            {
                allTokens.Add(token);
            }
        }

        IReadOnlyList<VocabularyCard> cards = [];
        if (allTokens.Count > 0)
        {
            cards = await _cardRepository.FindByAnyTokenAsync(allTokens.ToList(), cancellationToken);
        }

        var lookups = new Dictionary<string, VocabularyLookupResult>(StringComparer.OrdinalIgnoreCase);
        var cardsByToken = BuildCardsByToken(cards);

        foreach (var input in normalizedInputs)
        {
            var inputTokens = tokensByInput[input];
            var matches = inputTokens.Count == 0
                ? []
                : BuildMatches(cardsByToken, inputTokens);

            lookups[input] = new VocabularyLookupResult(input, matches);
        }

        return lookups;
    }

    public async Task IndexLookupResultAsync(
        VocabularyLookupResult lookup,
        VocabularyStorageMode storageMode,
        CancellationToken cancellationToken = default)
    {
        if (!lookup.Found)
        {
            return;
        }

        var storageModeText = storageMode.ToString().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in lookup.Matches)
        {
            await UpsertCardAsync(
                query: lookup.Query,
                entry.Word,
                entry.Meaning,
                entry.Examples,
                entry.DeckFileName,
                entry.DeckPath,
                entry.RowNumber,
                storageModeText,
                ExtractPartOfSpeechFromMeaning(entry.Meaning),
                VocabularySyncStatus.Synced,
                errorMessage: null,
                syncedAtUtc: now,
                now,
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task HandleAppendResultAsync(
        string requestedWord,
        string assistantReply,
        string? targetDeckFileName,
        string? overridePartOfSpeech,
        VocabularyAppendResult appendResult,
        VocabularyStorageMode storageMode,
        CancellationToken cancellationToken = default)
    {
        var storageModeText = storageMode.ToString().ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;

        if (appendResult.Status == VocabularyAppendStatus.Added && appendResult.Entry is not null)
        {
            var partOfSpeech = ResolvePartOfSpeech(assistantReply, overridePartOfSpeech, appendResult.Entry.Meaning);

            await UpsertCardAsync(
                query: requestedWord,
                appendResult.Entry.Word,
                appendResult.Entry.Meaning,
                appendResult.Entry.Examples,
                appendResult.Entry.DeckFileName,
                appendResult.Entry.DeckPath,
                appendResult.Entry.RowNumber,
                storageModeText,
                partOfSpeech,
                VocabularySyncStatus.Synced,
                errorMessage: null,
                syncedAtUtc: now,
                now,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        if (appendResult.Status == VocabularyAppendStatus.Error
            && VocabularyWriteFailurePolicy.ShouldQueueAfterInitialAppendError(appendResult.Message))
        {
            var targetDeck = !string.IsNullOrWhiteSpace(targetDeckFileName)
                ? targetDeckFileName.Trim()
                : appendResult.Entry?.DeckFileName;

            if (!string.IsNullOrWhiteSpace(targetDeck))
            {
                var normalizedRequestedWord = requestedWord.Trim();
                var normalizedOverridePartOfSpeech = NormalizeOptionalPartOfSpeech(overridePartOfSpeech);

                var existingActiveJob = await _syncJobRepository.FindActiveDuplicateAsync(
                    normalizedRequestedWord,
                    assistantReply.Trim(),
                    targetDeck,
                    storageModeText,
                    normalizedOverridePartOfSpeech,
                    cancellationToken);

                if (existingActiveJob is null)
                {
                    await _syncJobRepository.AddAsync(new VocabularySyncJob
                    {
                        RequestedWord = normalizedRequestedWord,
                        AssistantReply = assistantReply.Trim(),
                        TargetDeckFileName = targetDeck,
                        TargetDeckPath = appendResult.Entry?.DeckPath,
                        OverridePartOfSpeech = normalizedOverridePartOfSpeech,
                        StorageMode = storageModeText,
                        Status = VocabularySyncJobStatus.Pending,
                        AttemptCount = 0,
                        LastError = appendResult.Message,
                        CreatedAtUtc = now,
                        LastAttemptAtUtc = null,
                        CompletedAtUtc = null
                    }, cancellationToken);
                }
                else
                {
                    existingActiveJob.LastError = appendResult.Message;
                    if (string.IsNullOrWhiteSpace(existingActiveJob.TargetDeckPath))
                    {
                        existingActiveJob.TargetDeckPath = appendResult.Entry?.DeckPath;
                    }
                }

                if (_replyParser.TryParse(assistantReply, out var parsed) && parsed is not null)
                {
                    var pendingMeaning = string.Join(Environment.NewLine + Environment.NewLine, parsed.Meanings);
                    var pendingExamples = string.Join(Environment.NewLine + Environment.NewLine, parsed.Examples);
                    var pendingPos = ResolvePartOfSpeech(assistantReply, overridePartOfSpeech, pendingMeaning);

                    await UpsertCardAsync(
                        query: requestedWord,
                        parsed.Word,
                        pendingMeaning,
                        pendingExamples,
                        targetDeck,
                        appendResult.Entry?.DeckPath ?? string.Empty,
                        appendResult.Entry?.RowNumber ?? 0,
                        storageModeText,
                        pendingPos,
                        VocabularySyncStatus.Pending,
                        appendResult.Message,
                        syncedAtUtc: null,
                        now,
                        cancellationToken);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        if (appendResult.Status == VocabularyAppendStatus.Error)
        {
            _logger.LogWarning(
                "Vocabulary append failed and will not be queued by policy. Message: {Message}",
                appendResult.Message);
        }
    }

    private async Task UpsertCardAsync(
        string query,
        string word,
        string meaning,
        string examples,
        string deckFileName,
        string deckPath,
        int rowNumber,
        string storageMode,
        string? partOfSpeech,
        VocabularySyncStatus syncStatus,
        string? errorMessage,
        DateTimeOffset? syncedAtUtc,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var normalizedWord = NormalizeToken(word);
        if (string.IsNullOrWhiteSpace(normalizedWord))
        {
            return;
        }

        var card = await _cardRepository.GetByIdentityAsync(normalizedWord, deckFileName, storageMode, cancellationToken);
        if (card is null)
        {
            card = new VocabularyCard
            {
                Word = word.Trim(),
                NormalizedWord = normalizedWord,
                Meaning = meaning,
                Examples = examples,
                PartOfSpeechMarker = partOfSpeech,
                DeckFileName = deckFileName,
                DeckPath = deckPath,
                LastKnownRowNumber = rowNumber,
                StorageMode = storageMode,
                SyncStatus = syncStatus,
                LastSyncError = errorMessage,
                FirstSeenAtUtc = now,
                LastSeenAtUtc = now,
                SyncedAtUtc = syncedAtUtc
            };

            AddTokens(card, BuildTokens(word, query));
            await _cardRepository.AddAsync(card, cancellationToken);
            return;
        }

        card.Word = word.Trim();
        card.Meaning = meaning;
        card.Examples = examples;
        card.PartOfSpeechMarker = partOfSpeech;
        card.DeckPath = deckPath;
        card.LastKnownRowNumber = rowNumber;
        card.SyncStatus = syncStatus;
        card.LastSyncError = errorMessage;
        card.LastSeenAtUtc = now;

        if (syncedAtUtc.HasValue)
        {
            card.SyncedAtUtc = syncedAtUtc;
        }

        AddTokens(card, BuildTokens(word, query));
    }

    private static VocabularyDeckEntry MapToEntry(VocabularyCard card)
    {
        return new VocabularyDeckEntry(
            card.DeckFileName,
            card.DeckPath,
            card.LastKnownRowNumber,
            card.Word,
            card.Meaning,
            card.Examples);
    }

    private static IReadOnlyList<VocabularyDeckEntry> BuildMatches(
        IReadOnlyList<VocabularyCard> cards,
        IReadOnlyCollection<string> tokens)
    {
        var tokenSet = tokens.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return cards
            .Where(card => card.Tokens.Any(token => tokenSet.Contains(token.TokenNormalized)))
            .OrderByDescending(card => card.LastSeenAtUtc)
            .ThenBy(card => card.DeckFileName, StringComparer.OrdinalIgnoreCase)
            .Select(MapToEntry)
            .ToList();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<VocabularyCard>> BuildCardsByToken(
        IReadOnlyList<VocabularyCard> cards)
    {
        var map = new Dictionary<string, List<VocabularyCard>>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in cards)
        {
            foreach (var token in card.Tokens)
            {
                if (string.IsNullOrWhiteSpace(token.TokenNormalized))
                {
                    continue;
                }

                if (!map.TryGetValue(token.TokenNormalized, out var bucket))
                {
                    bucket = [];
                    map[token.TokenNormalized] = bucket;
                }

                bucket.Add(card);
            }
        }

        return map.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<VocabularyCard>)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<VocabularyDeckEntry> BuildMatches(
        IReadOnlyDictionary<string, IReadOnlyList<VocabularyCard>> cardsByToken,
        IReadOnlyCollection<string> tokens)
    {
        var matchedCards = new HashSet<VocabularyCard>();
        foreach (var token in tokens)
        {
            if (!cardsByToken.TryGetValue(token, out var cards))
            {
                continue;
            }

            foreach (var card in cards)
            {
                matchedCards.Add(card);
            }
        }

        return matchedCards
            .OrderByDescending(card => card.LastSeenAtUtc)
            .ThenBy(card => card.DeckFileName, StringComparer.OrdinalIgnoreCase)
            .Select(MapToEntry)
            .ToList();
    }

    private string? ResolvePartOfSpeech(string assistantReply, string? overridePartOfSpeech, string meaning)
    {
        if (!string.IsNullOrWhiteSpace(overridePartOfSpeech))
        {
            return NormalizeToken(overridePartOfSpeech);
        }

        if (_replyParser.TryParse(assistantReply, out var parsed) && parsed is not null)
        {
            var marker = parsed.PartsOfSpeech.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(marker))
            {
                return NormalizeToken(marker);
            }
        }

        return ExtractPartOfSpeechFromMeaning(meaning);
    }

    private static string? ExtractPartOfSpeechFromMeaning(string meaning)
    {
        if (string.IsNullOrWhiteSpace(meaning))
        {
            return null;
        }

        var lines = meaning
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var match = PosFromMeaningRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var pos = match.Groups["pos"].Value.Trim();
            return string.IsNullOrWhiteSpace(pos)
                ? null
                : NormalizeToken(pos);
        }

        return null;
    }

    private static string? NormalizeOptionalPartOfSpeech(string? overridePartOfSpeech)
    {
        if (string.IsNullOrWhiteSpace(overridePartOfSpeech))
        {
            return null;
        }

        var normalized = NormalizeToken(overridePartOfSpeech);
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized;
    }

    private static IReadOnlyList<string> BuildTokens(params string?[] values)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in values)
        {
            var normalized = NormalizeToken(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                tokens.Add(normalized);
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var parts = ReplaceDashVariants(value)
                .Split(WordFormSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var part in parts)
            {
                var partNormalized = NormalizeToken(part);
                if (!string.IsNullOrWhiteSpace(partNormalized))
                {
                    tokens.Add(partNormalized);
                }
            }
        }

        return tokens.ToList();
    }

    private static void AddTokens(VocabularyCard card, IReadOnlyList<string> tokens)
    {
        var existing = card.Tokens
            .Select(token => token.TokenNormalized)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var token in tokens)
        {
            if (!existing.Add(token))
            {
                continue;
            }

            card.Tokens.Add(new VocabularyCardToken
            {
                TokenNormalized = token
            });
        }
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = ReplaceDashVariants(value)
            .Trim()
            .Trim(TokenTrimChars);

        normalized = Regex.Replace(normalized, "\\s+", " ");
        return normalized.ToLowerInvariant();
    }

    private static string ReplaceDashVariants(string value)
    {
        var normalized = value;
        foreach (var dash in DashVariants)
        {
            normalized = normalized.Replace(dash, '-');
        }

        return normalized;
    }
}
