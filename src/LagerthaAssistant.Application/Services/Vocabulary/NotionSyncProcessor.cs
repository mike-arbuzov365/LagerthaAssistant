namespace LagerthaAssistant.Application.Services.Vocabulary;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Domain.Enums;
using Microsoft.Extensions.Logging;

public sealed class NotionSyncProcessor : INotionSyncProcessor
{
    private readonly IVocabularyCardRepository _cardRepository;
    private readonly INotionCardExportService _exportService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotionSyncProcessor> _logger;

    public NotionSyncProcessor(
        IVocabularyCardRepository cardRepository,
        INotionCardExportService exportService,
        IUnitOfWork unitOfWork,
        ILogger<NotionSyncProcessor> logger)
    {
        _cardRepository = cardRepository;
        _exportService = exportService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<NotionSyncStatusSummary> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var exportStatus = _exportService.GetStatus();
        var pending = await _cardRepository.CountPendingNotionSyncAsync(cancellationToken);
        var failed = await _cardRepository.CountFailedNotionSyncAsync(cancellationToken);

        return new NotionSyncStatusSummary(
            exportStatus.Enabled,
            exportStatus.IsConfigured,
            exportStatus.Message,
            pending,
            failed);
    }

    public async Task<NotionSyncRunSummary> ProcessPendingAsync(int take, CancellationToken cancellationToken = default)
    {
        var exportStatus = _exportService.GetStatus();
        if (!exportStatus.Enabled || !exportStatus.IsConfigured)
        {
            var pending = await _cardRepository.CountPendingNotionSyncAsync(cancellationToken);
            return new NotionSyncRunSummary(
                Requested: 0,
                Processed: 0,
                Completed: 0,
                Requeued: 0,
                Failed: 0,
                PendingAfterRun: pending);
        }

        var batchSize = Math.Max(1, take);
        var claimedAtUtc = DateTimeOffset.UtcNow;
        var cards = await _cardRepository.ClaimPendingNotionSyncAsync(batchSize, claimedAtUtc, cancellationToken);

        var requested = cards.Count;
        var processed = 0;
        var completed = 0;
        var requeued = 0;
        var failed = 0;

        foreach (var card in cards)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;

            NotionCardExportResult exportResult;
            try
            {
                exportResult = await _exportService.ExportAsync(MapExportRequest(card), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Notion export failed unexpectedly for vocabulary card #{CardId}", card.Id);
                exportResult = new NotionCardExportResult(
                    NotionCardExportOutcome.Failed,
                    IsRecoverableFailure: true,
                    ErrorMessage: ex.Message);
            }

            if (exportResult.Succeeded)
            {
                card.NotionSyncStatus = NotionSyncStatus.Synced;
                card.NotionLastError = null;
                card.NotionSyncedAtUtc = DateTimeOffset.UtcNow;
                if (!string.IsNullOrWhiteSpace(exportResult.PageId))
                {
                    card.NotionPageId = exportResult.PageId.Trim();
                }

                completed++;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                continue;
            }

            var shouldRequeue = exportResult.IsRecoverableFailure
                && NotionWriteFailurePolicy.ShouldRequeue(
                    exportResult.ErrorMessage,
                    card.NotionAttemptCount,
                    NotionSyncConstants.MaxRecoverableAttempts);

            if (shouldRequeue)
            {
                card.NotionSyncStatus = NotionSyncStatus.Pending;
                card.NotionLastError = exportResult.ErrorMessage;
                requeued++;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                continue;
            }

            card.NotionSyncStatus = NotionSyncStatus.Failed;
            card.NotionLastError = BuildRetryLimitOrTerminalError(
                exportResult.ErrorMessage,
                card.NotionAttemptCount);
            failed++;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var pendingAfterRun = await _cardRepository.CountPendingNotionSyncAsync(cancellationToken);

        if (processed > 0 || failed > 0 || requeued > 0)
        {
            _logger.LogInformation(
                "Notion sync processor run summary. Requested={Requested}, Processed={Processed}, Completed={Completed}, Requeued={Requeued}, Failed={Failed}, PendingAfterRun={PendingAfterRun}",
                requested,
                processed,
                completed,
                requeued,
                failed,
                pendingAfterRun);
        }
        else
        {
            _logger.LogDebug("Notion sync processor found no pending cards.");
        }

        return new NotionSyncRunSummary(
            Requested: requested,
            Processed: processed,
            Completed: completed,
            Requeued: requeued,
            Failed: failed,
            PendingAfterRun: pendingAfterRun);
    }

    public async Task<IReadOnlyList<NotionSyncFailedCard>> GetFailedCardsAsync(int take, CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take, 1, 500);
        var cards = await _cardRepository.GetFailedNotionSyncAsync(safeTake, cancellationToken);

        return cards
            .Select(card => new NotionSyncFailedCard(
                CardId: card.Id,
                Word: card.Word,
                DeckFileName: card.DeckFileName,
                StorageMode: card.StorageMode,
                AttemptCount: card.NotionAttemptCount,
                LastError: card.NotionLastError,
                LastAttemptAtUtc: card.NotionLastAttemptAtUtc,
                LastSeenAtUtc: card.LastSeenAtUtc))
            .ToList();
    }

    public async Task<int> RequeueFailedAsync(int take, CancellationToken cancellationToken = default)
    {
        var safeTake = Math.Clamp(take, 1, 500);
        var requeued = await _cardRepository.RequeueFailedNotionSyncAsync(safeTake, DateTimeOffset.UtcNow, cancellationToken);
        if (requeued > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Notion sync requeue run finished. RequeuedFailed={RequeuedFailed}", requeued);
        return requeued;
    }

    private static NotionCardExportRequest MapExportRequest(VocabularyCard card)
    {
        var key = $"{card.NormalizedWord}|{card.DeckFileName}|{card.StorageMode}";
        return new NotionCardExportRequest(
            CardId: card.Id,
            IdentityKey: key,
            Word: card.Word,
            Meaning: card.Meaning,
            Examples: card.Examples,
            PartOfSpeechMarker: card.PartOfSpeechMarker,
            DeckFileName: card.DeckFileName,
            StorageMode: card.StorageMode,
            RowNumber: card.LastKnownRowNumber,
            LastSeenAtUtc: card.LastSeenAtUtc,
            ExistingPageId: card.NotionPageId);
    }

    private static string BuildRetryLimitOrTerminalError(string? message, int attemptCount)
    {
        if (attemptCount < NotionSyncConstants.MaxRecoverableAttempts)
        {
            return message ?? "Notion sync failed.";
        }

        var safeMessage = string.IsNullOrWhiteSpace(message)
            ? "Recoverable failure."
            : message.Trim();

        return $"{safeMessage} Retry limit reached ({NotionSyncConstants.MaxRecoverableAttempts} attempts).";
    }
}

