namespace LagerthaAssistant.Application.Services.Vocabulary;

using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Domain.Enums;
using Microsoft.Extensions.Logging;

public sealed class VocabularySyncProcessor : IVocabularySyncProcessor
{
    private readonly IVocabularySyncJobRepository _syncJobRepository;
    private readonly IVocabularyDeckModeService _deckModeService;
    private readonly IVocabularyIndexService _indexService;
    private readonly IVocabularyStorageModeProvider _storageModeProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<VocabularySyncProcessor> _logger;

    public VocabularySyncProcessor(
        IVocabularySyncJobRepository syncJobRepository,
        IVocabularyDeckModeService deckModeService,
        IVocabularyIndexService indexService,
        IVocabularyStorageModeProvider storageModeProvider,
        IUnitOfWork unitOfWork,
        ILogger<VocabularySyncProcessor> logger)
    {
        _syncJobRepository = syncJobRepository;
        _deckModeService = deckModeService;
        _indexService = indexService;
        _storageModeProvider = storageModeProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
        => _syncJobRepository.CountPendingAsync(cancellationToken);

    public async Task<VocabularySyncRunSummary> ProcessPendingAsync(int take, CancellationToken cancellationToken = default)
    {
        var batchSize = Math.Max(1, take);
        var jobs = await _syncJobRepository.GetPendingAsync(batchSize, cancellationToken);

        var requested = jobs.Count;
        var processed = 0;
        var completed = 0;
        var requeued = 0;
        var failed = 0;

        foreach (var job in jobs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;

            if (!_storageModeProvider.TryParse(job.StorageMode, out var mode))
            {
                job.Status = VocabularySyncJobStatus.Failed;
                job.LastError = $"Unknown storage mode '{job.StorageMode}'.";
                job.LastAttemptAtUtc = DateTimeOffset.UtcNow;
                failed++;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                continue;
            }

            job.Status = VocabularySyncJobStatus.Processing;
            job.AttemptCount += 1;
            job.LastAttemptAtUtc = DateTimeOffset.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            VocabularyAppendResult appendResult;

            try
            {
                appendResult = await _deckModeService.AppendFromAssistantReplyAsync(
                    mode,
                    job.RequestedWord,
                    job.AssistantReply,
                    job.TargetDeckFileName,
                    job.OverridePartOfSpeech,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Pending vocabulary sync job failed unexpectedly for word {Word}", job.RequestedWord);
                job.Status = VocabularySyncJobStatus.Pending;
                job.LastError = ex.Message;
                requeued++;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                continue;
            }

            if (appendResult.Status == VocabularyAppendStatus.Added)
            {
                await _indexService.HandleAppendResultAsync(
                    job.RequestedWord,
                    job.AssistantReply,
                    job.TargetDeckFileName,
                    job.OverridePartOfSpeech,
                    appendResult,
                    mode,
                    cancellationToken);

                job.Status = VocabularySyncJobStatus.Completed;
                job.CompletedAtUtc = DateTimeOffset.UtcNow;
                job.LastError = null;
                completed++;

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                continue;
            }

            if (appendResult.Status == VocabularyAppendStatus.Error && IsRecoverableWriteFailure(appendResult.Message))
            {
                job.Status = VocabularySyncJobStatus.Pending;
                job.LastError = appendResult.Message;
                requeued++;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                continue;
            }

            job.Status = VocabularySyncJobStatus.Failed;
            job.LastError = appendResult.Message ?? $"Sync failed with status '{appendResult.Status}'.";
            failed++;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var pendingAfterRun = await _syncJobRepository.CountPendingAsync(cancellationToken);

        return new VocabularySyncRunSummary(
            Requested: requested,
            Processed: processed,
            Completed: completed,
            Requeued: requeued,
            Failed: failed,
            PendingAfterRun: pendingAfterRun);
    }

    private static bool IsRecoverableWriteFailure(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("open in another app", StringComparison.OrdinalIgnoreCase)
            || message.Contains("currently in use", StringComparison.OrdinalIgnoreCase)
            || message.Contains("file is locked", StringComparison.OrdinalIgnoreCase)
            || message.Contains("locked right now", StringComparison.OrdinalIgnoreCase)
            || message.Contains("version conflict", StringComparison.OrdinalIgnoreCase);
    }
}



