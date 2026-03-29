namespace SharedBotKernel.Workers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public abstract class BackgroundSyncWorkerBase<TJob> : BackgroundService
    where TJob : class
{
    protected abstract ILogger Logger { get; }
    protected abstract int IntervalSeconds { get; }
    protected abstract int BatchSize { get; }
    protected abstract int MaxBackoffSeconds { get; }
    protected abstract double BackoffFactor { get; }

    protected abstract Task<IReadOnlyList<TJob>> ClaimPendingJobsAsync(IServiceScope scope, int batchSize, CancellationToken ct);
    protected abstract Task ProcessJobAsync(IServiceScope scope, TJob job, CancellationToken ct);
    protected abstract Task MarkFailedAsync(IServiceScope scope, TJob job, string error, CancellationToken ct);

    protected readonly IServiceScopeFactory ScopeFactory;

    protected BackgroundSyncWorkerBase(IServiceScopeFactory scopeFactory)
    {
        ScopeFactory = scopeFactory;
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = Math.Clamp(IntervalSeconds, 1, 3600);
        var batchSize = Math.Clamp(BatchSize, 1, 500);
        var backoffFactor = Math.Clamp((int)BackoffFactor, 2, 4);
        var maxBackoffSeconds = Math.Clamp(MaxBackoffSeconds, intervalSeconds, 86400);

        var failureStreak = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = CalculateDelay(intervalSeconds, maxBackoffSeconds, backoffFactor, failureStreak);
            Logger.LogDebug(
                "Next {WorkerType} run in {DelaySeconds}s (FailureStreak={FailureStreak}).",
                GetType().Name,
                delay.TotalSeconds,
                failureStreak);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            var runSucceeded = await RunOnceAsync(batchSize, stoppingToken);
            failureStreak = runSucceeded
                ? 0
                : Math.Min(failureStreak + 1, 10);
        }
    }

    private async Task<bool> RunOnceAsync(int batchSize, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = ScopeFactory.CreateScope();
            var jobs = await ClaimPendingJobsAsync(scope, batchSize, stoppingToken);
            foreach (var job in jobs)
            {
                try
                {
                    await ProcessJobAsync(scope, job, stoppingToken);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Job processing failed: {JobType}", typeof(TJob).Name);
                    try
                    {
                        await MarkFailedAsync(scope, job, ex.Message, stoppingToken);
                    }
                    catch
                    {
                        // swallow
                    }
                }
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Worker iteration failed: {WorkerType}", GetType().Name);
            return false;
        }
    }
}
