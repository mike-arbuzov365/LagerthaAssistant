using System.Diagnostics;
using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using Microsoft.Extensions.Options;

namespace LagerthaAssistant.Api.HostedServices;

public sealed class NotionSyncHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotionSyncHostedService> _logger;
    private readonly NotionSyncWorkerOptions _options;

    public NotionSyncHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<NotionSyncWorkerOptions> options,
        ILogger<NotionSyncHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Notion sync worker is disabled.");
            return;
        }

        var intervalSeconds = Math.Clamp(_options.IntervalSeconds, 1, 3600);
        var batchSize = Math.Clamp(_options.BatchSize, 1, 500);
        var backoffFactor = Math.Clamp(_options.BackoffFactor, 2, 4);
        var maxBackoffSeconds = Math.Clamp(_options.MaxBackoffSeconds, intervalSeconds, 86400);

        _logger.LogInformation(
            "Notion sync worker started. Interval={IntervalSeconds}s, BatchSize={BatchSize}, RunOnStartup={RunOnStartup}, MaxBackoff={MaxBackoffSeconds}s, BackoffFactor={BackoffFactor}",
            intervalSeconds,
            batchSize,
            _options.RunOnStartup,
            maxBackoffSeconds,
            backoffFactor);

        var failureStreak = 0;
        var isFirstRun = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!isFirstRun || !_options.RunOnStartup)
            {
                var delay = CalculateDelay(intervalSeconds, maxBackoffSeconds, backoffFactor, failureStreak);
                _logger.LogDebug(
                    "Next Notion sync run in {DelaySeconds}s (FailureStreak={FailureStreak}).",
                    delay.TotalSeconds,
                    failureStreak);

                await Task.Delay(delay, stoppingToken);
            }

            isFirstRun = false;

            var runSucceeded = await RunOnceAsync(batchSize, stoppingToken);
            failureStreak = runSucceeded
                ? 0
                : Math.Min(failureStreak + 1, 10);
        }
    }

    public static TimeSpan CalculateDelay(
        int intervalSeconds,
        int maxBackoffSeconds,
        int backoffFactor,
        int failureStreak)
    {
        var safeInterval = Math.Clamp(intervalSeconds, 1, 3600);
        var safeMaxBackoff = Math.Clamp(maxBackoffSeconds, safeInterval, 86400);
        var safeBackoffFactor = Math.Clamp(backoffFactor, 2, 4);
        var safeFailures = Math.Max(0, failureStreak);

        var multiplier = 1.0;
        for (var i = 0; i < safeFailures; i++)
        {
            multiplier *= safeBackoffFactor;
            if (safeInterval * multiplier >= safeMaxBackoff)
            {
                return TimeSpan.FromSeconds(safeMaxBackoff);
            }
        }

        var delaySeconds = Math.Min(safeMaxBackoff, (int)Math.Round(safeInterval * multiplier));
        return TimeSpan.FromSeconds(delaySeconds);
    }

    private async Task<bool> RunOnceAsync(int batchSize, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var syncProcessor = scope.ServiceProvider.GetRequiredService<INotionSyncProcessor>();

            var statusBefore = await syncProcessor.GetStatusAsync(cancellationToken);
            var summary = await syncProcessor.ProcessPendingAsync(batchSize, cancellationToken);
            stopwatch.Stop();

            if (summary.Processed > 0 || statusBefore.PendingCards > 0 || summary.Failed > 0)
            {
                _logger.LogInformation(
                    "Notion sync run completed in {ElapsedMs} ms. PendingBefore={PendingBefore}, Requested={Requested}, Processed={Processed}, Completed={Completed}, Requeued={Requeued}, Failed={Failed}, PendingAfter={PendingAfter}",
                    stopwatch.ElapsedMilliseconds,
                    statusBefore.PendingCards,
                    summary.Requested,
                    summary.Processed,
                    summary.Completed,
                    summary.Requeued,
                    summary.Failed,
                    summary.PendingAfterRun);
            }
            else
            {
                _logger.LogDebug(
                    "Notion sync run completed in {ElapsedMs} ms with no pending cards.",
                    stopwatch.ElapsedMilliseconds);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown.
            return true;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "Notion sync run failed after {ElapsedMs} ms. Backoff will be applied.",
                stopwatch.ElapsedMilliseconds);
            return false;
        }
    }
}

