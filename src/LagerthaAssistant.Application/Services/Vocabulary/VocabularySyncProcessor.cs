namespace LagerthaAssistant.Application.Services.Vocabulary;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
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

    public async Task<IReadOnlyList<VocabularySyncFailedJob>> GetFailedJobsAsync(int take, CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take, 1, 500);
        var jobs = await _syncJobRepository.GetFailedAsync(safeTake, cancellationToken);

        return jobs
            .Select(job => new VocabularySyncFailedJob(
                job.Id,
                job.RequestedWord,
                job.TargetDeckFileName,
                job.StorageMode,
                job.AttemptCount,
                job.LastError,
                job.LastAttemptAtUtc,
                job.CreatedAtUtc))
            .ToList();
    }

    public async Task<int> RequeueFailedAsync(int take, CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take, 1, 500);
        var requeued = await _syncJobRepository.RequeueFailedAsync(safeTake, DateTimeOffset.UtcNow, cancellationToken);
        if (requeued > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Vocabulary sync requeue run finished. RequeuedFailed={RequeuedFailed}", requeued);
        return requeued;
    }

    public async Task<VocabularySyncRunSummary> ProcessPendingAsync(int take, CancellationToken cancellationToken = default)
    {
        var batchSize = Math.Max(1, take);
        var claimedAtUtc = DateTimeOffset.UtcNow;
        var jobs = await _syncJobRepository.ClaimPendingAsync(batchSize, claimedAtUtc, cancellationToken);

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
                failed++;
                continue;
            }

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

                if (VocabularyWriteFailurePolicy.ShouldRequeueQueuedJob(
                    ex.Message,
                    job.AttemptCount,
                    VocabularySyncConstants.MaxRecoverableAttempts))
                {
                    job.Status = VocabularySyncJobStatus.Pending;
                    job.LastError = ex.Message;
                    requeued++;
                }
                else
                {
                    job.Status = VocabularySyncJobStatus.Failed;
                    job.LastError = BuildRetryLimitOrTerminalError(ex.Message, job.AttemptCount);
                    failed++;
                }

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
                continue;
            }

            if (appendResult.Status == VocabularyAppendStatus.Error
                && VocabularyWriteFailurePolicy.ShouldRequeueQueuedJob(
                    appendResult.Message,
                    job.AttemptCount,
                    VocabularySyncConstants.MaxRecoverableAttempts))
            {
                job.Status = VocabularySyncJobStatus.Pending;
                job.LastError = appendResult.Message;
                requeued++;
                continue;
            }

            job.Status = VocabularySyncJobStatus.Failed;
            job.LastError = BuildRetryLimitOrTerminalError(
                appendResult.Message ?? $"Sync failed with status '{appendResult.Status}'.",
                job.AttemptCount);
            failed++;
        }

        if (processed > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var pendingAfterRun = await _syncJobRepository.CountPendingAsync(cancellationToken);

        if (processed > 0 || failed > 0 || requeued > 0)
        {
            _logger.LogInformation(
                "Vocabulary sync processor run summary. Requested={Requested}, Processed={Processed}, Completed={Completed}, Requeued={Requeued}, Failed={Failed}, PendingAfterRun={PendingAfterRun}",
                requested,
                processed,
                completed,
                requeued,
                failed,
                pendingAfterRun);
        }
        else
        {
            _logger.LogDebug("Vocabulary sync processor found no pending jobs.");
        }

        return new VocabularySyncRunSummary(
            Requested: requested,
            Processed: processed,
            Completed: completed,
            Requeued: requeued,
            Failed: failed,
            PendingAfterRun: pendingAfterRun);
    }

    private static string BuildRetryLimitOrTerminalError(string? message, int attemptCount)
    {
        if (attemptCount < VocabularySyncConstants.MaxRecoverableAttempts)
        {
            return message ?? "Vocabulary sync job failed.";
        }

        var safeMessage = string.IsNullOrWhiteSpace(message)
            ? "Recoverable failure."
            : message.Trim();

        return $"{safeMessage} Retry limit reached ({VocabularySyncConstants.MaxRecoverableAttempts} attempts).";
    }
}
