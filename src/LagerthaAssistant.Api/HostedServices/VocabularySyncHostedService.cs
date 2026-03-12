using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using Microsoft.Extensions.Options;

namespace LagerthaAssistant.Api.HostedServices;

public sealed class VocabularySyncHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VocabularySyncHostedService> _logger;
    private readonly VocabularySyncWorkerOptions _options;

    public VocabularySyncHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<VocabularySyncWorkerOptions> options,
        ILogger<VocabularySyncHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Vocabulary sync worker is disabled.");
            return;
        }

        var intervalSeconds = Math.Clamp(_options.IntervalSeconds, 5, 3600);
        var batchSize = Math.Clamp(_options.BatchSize, 1, 500);

        _logger.LogInformation(
            "Vocabulary sync worker started. Interval={IntervalSeconds}s, BatchSize={BatchSize}, RunOnStartup={RunOnStartup}",
            intervalSeconds,
            batchSize,
            _options.RunOnStartup);

        if (_options.RunOnStartup)
        {
            await RunOnceAsync(batchSize, stoppingToken);
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(batchSize, stoppingToken);
        }
    }

    private async Task RunOnceAsync(int batchSize, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var syncProcessor = scope.ServiceProvider.GetRequiredService<IVocabularySyncProcessor>();

            var summary = await syncProcessor.ProcessPendingAsync(batchSize, cancellationToken);

            if (summary.Processed > 0 || summary.PendingAfterRun > 0)
            {
                _logger.LogInformation(
                    "Vocabulary sync run completed. Requested={Requested}, Processed={Processed}, Completed={Completed}, Requeued={Requeued}, Failed={Failed}, Pending={Pending}",
                    summary.Requested,
                    summary.Processed,
                    summary.Completed,
                    summary.Requeued,
                    summary.Failed,
                    summary.PendingAfterRun);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vocabulary sync run failed.");
        }
    }
}
