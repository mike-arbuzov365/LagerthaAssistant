namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

using System.Text.RegularExpressions;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Infrastructure.Options;
using Microsoft.Extensions.Logging;

public sealed class GraphVocabularyDeckService : IVocabularyDeckBackend, IVocabularyBatchDeckLookupBackend, IAsyncDisposable
{
    private readonly VocabularyDeckOptions _options;
    private readonly IVocabularyReplyParser _replyParser;
    private readonly IGraphDriveClient _graphDriveClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<GraphVocabularyDeckService> _logger;
    private readonly Regex _filePatternRegex;
    private readonly SemaphoreSlim _operationSync = new(1, 1);
    private int _disposeState;

    private MirrorContext? _sessionMirror;
    private CachedAppendPlan? _cachedAppendPlan;
    private PendingUpload? _pendingUpload;

    public GraphVocabularyDeckService(
        VocabularyDeckOptions options,
        IVocabularyReplyParser replyParser,
        IGraphDriveClient graphDriveClient,
        ILoggerFactory loggerFactory,
        ILogger<GraphVocabularyDeckService> logger)
    {
        _options = options;
        _replyParser = replyParser;
        _graphDriveClient = graphDriveClient;
        _loggerFactory = loggerFactory;
        _logger = logger;

        var pattern = string.IsNullOrWhiteSpace(options.FilePattern)
            ? "wm-*.xlsx"
            : options.FilePattern;

        _filePatternRegex = BuildWildcardRegex(pattern);
    }

    public VocabularyStorageMode Mode => VocabularyStorageMode.Graph;

    public async Task<VocabularyLookupResult> FindInWritableDecksAsync(string word, CancellationToken cancellationToken = default)
    {
        var normalizedWord = VocabularyAppendPlanning.NormalizeWord(word);

        await _operationSync.WaitAsync(cancellationToken);
        try
        {
            var mirror = await GetOrCreateMirrorAsync(cancellationToken);
            return await mirror.LocalService.FindInWritableDecksAsync(word, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Graph lookup failed. Returning empty result.");
            return new VocabularyLookupResult(normalizedWord, []);
        }
        finally
        {
            _operationSync.Release();
        }
    }

    public async Task<IReadOnlyDictionary<string, VocabularyLookupResult>> FindInWritableDecksBatchAsync(
        IReadOnlyList<string> words,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(words);

        var batchWords = words
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Select(word => word.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (batchWords.Count == 0)
        {
            return new Dictionary<string, VocabularyLookupResult>(StringComparer.OrdinalIgnoreCase);
        }

        await _operationSync.WaitAsync(cancellationToken);
        try
        {
            var mirror = await GetOrCreateMirrorAsync(cancellationToken);
            return await mirror.LocalService.FindInWritableDecksBatchAsync(batchWords, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Graph batch lookup failed. Returning empty results.");

            return batchWords.ToDictionary(
                word => word,
                word => new VocabularyLookupResult(word, []),
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            _operationSync.Release();
        }
    }

    public async Task<IReadOnlyList<VocabularyDeckFile>> GetWritableDeckFilesAsync(CancellationToken cancellationToken = default)
    {
        await _operationSync.WaitAsync(cancellationToken);
        try
        {
            var mirror = await GetOrCreateMirrorAsync(cancellationToken);
            return mirror.RemoteFilesByName.Values
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => new VocabularyDeckFile(x.Name, x.FullPath))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list writable OneDrive decks.");
            return [];
        }
        finally
        {
            _operationSync.Release();
        }
    }

    public async Task<VocabularyAppendPreviewResult> PreviewAppendFromAssistantReplyAsync(
        string requestedWord,
        string assistantReply,
        string? forcedDeckFileName = null,
        string? overridePartOfSpeech = null,
        CancellationToken cancellationToken = default)
    {
        await _operationSync.WaitAsync(cancellationToken);
        try
        {
            var mirror = await GetOrCreateMirrorAsync(cancellationToken);

            var preview = await mirror.LocalService.PreviewAppendFromAssistantReplyAsync(
                requestedWord,
                assistantReply,
                forcedDeckFileName,
                overridePartOfSpeech,
                cancellationToken);

            var remappedPreview = RemapPreviewPath(preview, mirror);
            CacheAppendPlan(
                remappedPreview,
                requestedWord,
                assistantReply,
                forcedDeckFileName,
                overridePartOfSpeech);

            _pendingUpload = null;
            return remappedPreview;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Graph preview append failed.");
            _cachedAppendPlan = null;
            _pendingUpload = null;

            return new VocabularyAppendPreviewResult(
                VocabularyAppendPreviewStatus.NoWritableDecks,
                VocabularyAppendPlanning.NormalizeWord(requestedWord),
                Message: ex.Message);
        }
        finally
        {
            _operationSync.Release();
        }
    }

    public async Task<VocabularyAppendResult> AppendFromAssistantReplyAsync(
        string requestedWord,
        string assistantReply,
        string? forcedDeckFileName = null,
        string? overridePartOfSpeech = null,
        CancellationToken cancellationToken = default)
    {
        await _operationSync.WaitAsync(cancellationToken);
        try
        {
            var mirror = await GetOrCreateMirrorAsync(cancellationToken);
            var signature = VocabularyAppendPlanning.CreateSignature(requestedWord, assistantReply, forcedDeckFileName, overridePartOfSpeech);

            if (_pendingUpload is not null && !_pendingUpload.Signature.Equals(signature))
            {
                _pendingUpload = null;
            }

            if (_pendingUpload is not null && _pendingUpload.Signature.Equals(signature))
            {
                return await TryUploadPendingAsync(_pendingUpload, mirror, cancellationToken);
            }

            var appendResult = await AppendUsingPlanOrFallbackAsync(
                signature,
                requestedWord,
                assistantReply,
                forcedDeckFileName,
                overridePartOfSpeech,
                mirror,
                cancellationToken);

            if (appendResult.Status != VocabularyAppendStatus.Added || appendResult.Entry is null)
            {
                return appendResult;
            }

            if (!mirror.RemoteFilesByName.TryGetValue(appendResult.Entry.DeckFileName, out var remoteFile))
            {
                _cachedAppendPlan = null;
                _pendingUpload = null;

                return new VocabularyAppendResult(
                    VocabularyAppendStatus.Error,
                    Message: $"Could not resolve OneDrive target deck '{appendResult.Entry.DeckFileName}'.");
            }

            var uploadResult = await UploadLocalDeckCopyAsync(remoteFile, mirror, cancellationToken);
            if (!uploadResult.Succeeded)
            {
                if (IsVersionConflict(uploadResult.Message))
                {
                    _pendingUpload = null;
                    await InvalidateMirrorCoreAsync();
                }
                else if (IsFileLocked(uploadResult.Message))
                {
                    _pendingUpload = PendingUpload.From(signature, appendResult.Entry, remoteFile);
                }
                else
                {
                    _pendingUpload = null;
                }

                return new VocabularyAppendResult(
                    VocabularyAppendStatus.Error,
                    Message: uploadResult.Message ?? "Failed to upload updated deck to OneDrive.");
            }

            _pendingUpload = null;
            mirror.UpdateRemoteFileETag(remoteFile.Name, uploadResult.UpdatedETag);

            var updatedRemoteFile = mirror.RemoteFilesByName.TryGetValue(remoteFile.Name, out var refreshed)
                ? refreshed
                : remoteFile;

            var remappedEntry = new VocabularyDeckEntry(
                appendResult.Entry.DeckFileName,
                updatedRemoteFile.FullPath,
                appendResult.Entry.RowNumber,
                appendResult.Entry.Word,
                appendResult.Entry.Meaning,
                appendResult.Entry.Examples);

            return appendResult with { Entry = remappedEntry };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Graph append failed.");
            _pendingUpload = null;
            _cachedAppendPlan = null;

            return new VocabularyAppendResult(
                VocabularyAppendStatus.Error,
                Message: ex.Message);
        }
        finally
        {
            _operationSync.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        var lockTaken = false;

        try
        {
            await _operationSync.WaitAsync();
            lockTaken = true;
            await InvalidateMirrorCoreAsync();
            _cachedAppendPlan = null;
            _pendingUpload = null;
        }
        catch (ObjectDisposedException)
        {
            // Scope disposal can invoke async dispose after nested dependencies
            // were already torn down. We treat this as an already-disposed case.
        }
        finally
        {
            if (lockTaken)
            {
                _operationSync.Release();
            }

            _operationSync.Dispose();
        }
    }

    private async Task<VocabularyAppendResult> AppendUsingPlanOrFallbackAsync(
        VocabularyAppendRequestSignature signature,
        string requestedWord,
        string assistantReply,
        string? forcedDeckFileName,
        string? overridePartOfSpeech,
        MirrorContext mirror,
        CancellationToken cancellationToken)
    {
        if (_cachedAppendPlan is not null && _cachedAppendPlan.Signature.Equals(signature))
        {
            var preparedAppend = await mirror.LocalService.AppendPreparedCardAsync(
                _cachedAppendPlan.TargetWord,
                _cachedAppendPlan.MeaningText,
                _cachedAppendPlan.ExamplesText,
                _cachedAppendPlan.TargetDeckFileName,
                cancellationToken);

            return preparedAppend;
        }

        return await mirror.LocalService.AppendFromAssistantReplyAsync(
            requestedWord,
            assistantReply,
            forcedDeckFileName,
            overridePartOfSpeech,
            cancellationToken);
    }

    public async Task<IReadOnlyList<VocabularyDeckEntry>> GetAllEntriesAsync(CancellationToken cancellationToken = default)
    {
        await _operationSync.WaitAsync(cancellationToken);
        try
        {
            var mirror = await GetOrCreateMirrorAsync(cancellationToken);
            return await mirror.LocalService.GetAllEntriesAsync(cancellationToken);
        }
        finally
        {
            _operationSync.Release();
        }
    }

    private async Task<GraphUploadResult> UploadLocalDeckCopyAsync(
        GraphDriveFile remoteFile,
        MirrorContext mirror,
        CancellationToken cancellationToken)
    {
        var localPath = Path.Combine(mirror.TempFolderPath, remoteFile.Name);
        var updatedBytes = await File.ReadAllBytesAsync(localPath, cancellationToken);

        return await _graphDriveClient.UploadFileContentAsync(
            remoteFile.Id,
            updatedBytes,
            remoteFile.ETag,
            cancellationToken);
    }

    private async Task<VocabularyAppendResult> TryUploadPendingAsync(
        PendingUpload pendingUpload,
        MirrorContext mirror,
        CancellationToken cancellationToken)
    {
        if (!mirror.RemoteFilesByName.TryGetValue(pendingUpload.DeckFileName, out var remoteFile))
        {
            _pendingUpload = null;

            return new VocabularyAppendResult(
                VocabularyAppendStatus.Error,
                Message: $"Could not resolve OneDrive target deck '{pendingUpload.DeckFileName}'.");
        }

        var uploadTarget = remoteFile with { ETag = pendingUpload.ExpectedETag };
        var uploadResult = await UploadLocalDeckCopyAsync(uploadTarget, mirror, cancellationToken);

        if (!uploadResult.Succeeded)
        {
            if (IsVersionConflict(uploadResult.Message))
            {
                _pendingUpload = null;
                await InvalidateMirrorCoreAsync();
            }
            else if (IsFileLocked(uploadResult.Message))
            {
                _pendingUpload = pendingUpload;
            }
            else
            {
                _pendingUpload = null;
            }

            return new VocabularyAppendResult(
                VocabularyAppendStatus.Error,
                Message: uploadResult.Message ?? "Failed to upload updated deck to OneDrive.");
        }

        _pendingUpload = null;
        mirror.UpdateRemoteFileETag(remoteFile.Name, uploadResult.UpdatedETag);

        var entry = new VocabularyDeckEntry(
            pendingUpload.DeckFileName,
            pendingUpload.DeckPath,
            pendingUpload.RowNumber,
            pendingUpload.Word,
            pendingUpload.Meaning,
            pendingUpload.Examples);

        return new VocabularyAppendResult(VocabularyAppendStatus.Added, Entry: entry);
    }

    private void CacheAppendPlan(
        VocabularyAppendPreviewResult preview,
        string requestedWord,
        string assistantReply,
        string? forcedDeckFileName,
        string? overridePartOfSpeech)
    {
        if (preview.Status != VocabularyAppendPreviewStatus.ReadyToAppend
            || string.IsNullOrWhiteSpace(preview.TargetDeckFileName))
        {
            _cachedAppendPlan = null;
            return;
        }

        var signature = VocabularyAppendPlanning.CreateSignature(requestedWord, assistantReply, forcedDeckFileName, overridePartOfSpeech);

        if (!VocabularyAppendPlanning.TryBuildPayload(_replyParser, requestedWord, assistantReply, overridePartOfSpeech, out var payload))
        {
            _cachedAppendPlan = null;
            return;
        }

        _cachedAppendPlan = new CachedAppendPlan(
            signature,
            payload.TargetWord,
            payload.MeaningText,
            payload.ExamplesText,
            preview.TargetDeckFileName);
    }

    private static bool IsVersionConflict(string? message)
    {
        return !string.IsNullOrWhiteSpace(message)
            && message.Contains("version conflict", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFileLocked(string? message)
    {
        return !string.IsNullOrWhiteSpace(message)
            && message.Contains("locked", StringComparison.OrdinalIgnoreCase);
    }

    private VocabularyAppendPreviewResult RemapPreviewPath(VocabularyAppendPreviewResult preview, MirrorContext mirror)
    {
        if (preview.Status != VocabularyAppendPreviewStatus.ReadyToAppend
            || string.IsNullOrWhiteSpace(preview.TargetDeckFileName))
        {
            return preview;
        }

        if (!mirror.RemoteFilesByName.TryGetValue(preview.TargetDeckFileName, out var remoteFile))
        {
            return preview;
        }

        return preview with { TargetDeckPath = remoteFile.FullPath };
    }

    private async Task<MirrorContext> GetOrCreateMirrorAsync(CancellationToken cancellationToken)
    {
        if (_sessionMirror is not null)
        {
            return _sessionMirror;
        }

        _sessionMirror = await CreateMirrorAsync(cancellationToken);
        _cachedAppendPlan = null;
        _pendingUpload = null;

        return _sessionMirror;
    }

    private async Task InvalidateMirrorCoreAsync()
    {
        var mirror = _sessionMirror;
        _sessionMirror = null;

        if (mirror is not null)
        {
            await mirror.DisposeAsync();
        }

        _cachedAppendPlan = null;
        _pendingUpload = null;
    }

    private async Task<MirrorContext> CreateMirrorAsync(CancellationToken cancellationToken)
    {
        var remoteFiles = await GetWritableRemoteFilesAsync(cancellationToken);
        if (remoteFiles.Count == 0)
        {
            throw new InvalidOperationException("No writable OneDrive vocabulary decks found.");
        }

        var tempFolder = Path.Combine(Path.GetTempPath(), $"lagertha-graph-decks-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempFolder);

        foreach (var remoteFile in remoteFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = await _graphDriveClient.DownloadFileContentAsync(remoteFile.Id, cancellationToken);
            var localPath = Path.Combine(tempFolder, remoteFile.Name);
            await File.WriteAllBytesAsync(localPath, bytes, cancellationToken);
        }

        var tempOptions = new VocabularyDeckOptions
        {
            FolderPath = tempFolder,
            FilePattern = _options.FilePattern,
            ReadOnlyFileNames = [],
            NounDeckFileName = _options.NounDeckFileName,
            VerbDeckFileName = _options.VerbDeckFileName,
            IrregularVerbDeckFileName = _options.IrregularVerbDeckFileName,
            PhrasalVerbDeckFileName = _options.PhrasalVerbDeckFileName,
            AdjectiveDeckFileName = _options.AdjectiveDeckFileName,
            AdverbDeckFileName = _options.AdverbDeckFileName,
            PrepositionDeckFileName = _options.PrepositionDeckFileName,
            ConjunctionDeckFileName = _options.ConjunctionDeckFileName,
            PronounDeckFileName = _options.PronounDeckFileName,
            PersistentExpressionDeckFileName = _options.PersistentExpressionDeckFileName,
            FallbackDeckFileName = _options.FallbackDeckFileName
        };

        var localService = new VocabularyDeckService(
            tempOptions,
            _replyParser,
            _loggerFactory.CreateLogger<VocabularyDeckService>());

        var remoteMap = remoteFiles.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        return new MirrorContext(tempFolder, localService, remoteMap);
    }

    private async Task<IReadOnlyList<GraphDriveFile>> GetWritableRemoteFilesAsync(CancellationToken cancellationToken)
    {
        var allFiles = await _graphDriveClient.ListFilesAsync(cancellationToken);
        var readOnlyNames = new HashSet<string>(_options.ReadOnlyFileNames ?? [], StringComparer.OrdinalIgnoreCase);

        return allFiles
            .Where(file => _filePatternRegex.IsMatch(file.Name))
            .Where(file => !IsReadOnlyDeck(file.Name, readOnlyNames))
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsReadOnlyDeck(string fileName, ISet<string> readOnlyNames)
    {
        return readOnlyNames.Contains(fileName)
            || fileName.Contains("-all-", StringComparison.OrdinalIgnoreCase);
    }

    private static Regex BuildWildcardRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".");

        return new Regex($"^{escaped}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    private sealed record CachedAppendPlan(
        VocabularyAppendRequestSignature Signature,
        string TargetWord,
        string MeaningText,
        string ExamplesText,
        string TargetDeckFileName);

    private sealed record PendingUpload(
        VocabularyAppendRequestSignature Signature,
        string DeckFileName,
        string DeckPath,
        int RowNumber,
        string Word,
        string Meaning,
        string Examples,
        string ExpectedETag)
    {
        public static PendingUpload From(VocabularyAppendRequestSignature signature, VocabularyDeckEntry entry, GraphDriveFile remoteFile)
        {
            return new PendingUpload(
                signature,
                entry.DeckFileName,
                remoteFile.FullPath,
                entry.RowNumber,
                entry.Word,
                entry.Meaning,
                entry.Examples,
                remoteFile.ETag);
        }
    }

    private sealed class MirrorContext : IAsyncDisposable
    {
        public MirrorContext(
            string tempFolderPath,
            VocabularyDeckService localService,
            Dictionary<string, GraphDriveFile> remoteFilesByName)
        {
            TempFolderPath = tempFolderPath;
            LocalService = localService;
            RemoteFilesByName = remoteFilesByName;
        }

        public string TempFolderPath { get; }

        public VocabularyDeckService LocalService { get; }

        public Dictionary<string, GraphDriveFile> RemoteFilesByName { get; }

        public void UpdateRemoteFileETag(string fileName, string? updatedETag)
        {
            if (!RemoteFilesByName.TryGetValue(fileName, out var file))
            {
                return;
            }

            var normalizedETag = updatedETag?.Trim() ?? string.Empty;
            RemoteFilesByName[fileName] = file with { ETag = normalizedETag };
        }

        public ValueTask DisposeAsync()
        {
            try
            {
                if (Directory.Exists(TempFolderPath))
                {
                    Directory.Delete(TempFolderPath, true);
                }
            }
            catch
            {
                // Ignore cleanup errors.
            }

            return ValueTask.CompletedTask;
        }
    }
}






