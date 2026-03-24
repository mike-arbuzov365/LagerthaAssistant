namespace LagerthaAssistant.Api.HostedServices;

using System.Diagnostics;
using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Application.Interfaces.Food;
using Microsoft.Extensions.Options;

public sealed class FoodSyncHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FoodSyncHostedService> _logger;
    private readonly FoodSyncWorkerOptions _options;

    public FoodSyncHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<FoodSyncWorkerOptions> options,
        ILogger<FoodSyncHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Food sync worker is disabled.");
            return;
        }

        var fromNotionInterval = TimeSpan.FromSeconds(Math.Clamp(_options.SyncFromNotionIntervalSeconds, 30, 86400));
        var toNotionInterval = TimeSpan.FromSeconds(Math.Clamp(_options.SyncToNotionIntervalSeconds, 5, 3600));
        var batchSize = Math.Clamp(_options.BatchSize, 1, 500);
        var backoffFactor = Math.Clamp(_options.BackoffFactor, 2, 4);
        var maxBackoff = TimeSpan.FromSeconds(Math.Clamp(_options.MaxBackoffSeconds, 60, 86400));

        _logger.LogInformation(
            "Food sync worker started. FromNotion={FromNotion}s, ToNotion={ToNotion}s, Batch={Batch}, RunOnStartup={RunOnStartup}",
            fromNotionInterval.TotalSeconds,
            toNotionInterval.TotalSeconds,
            batchSize,
            _options.RunOnStartup);

        var fromNotionFailureStreak = 0;
        var nextFromNotion = _options.RunOnStartup
            ? DateTime.UtcNow
            : DateTime.UtcNow.Add(fromNotionInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(toNotionInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Push local changes to Notion.
            await RunToNotionAsync(batchSize, stoppingToken);

            // Pull from Notion on its own slower cadence.
            if (DateTime.UtcNow >= nextFromNotion)
            {
                var succeeded = await RunFromNotionAsync(stoppingToken);
                fromNotionFailureStreak = succeeded ? 0 : Math.Min(fromNotionFailureStreak + 1, 10);
                var backoffMultiplier = Math.Pow(backoffFactor, fromNotionFailureStreak);
                var backoffSeconds = Math.Min(maxBackoff.TotalSeconds, fromNotionInterval.TotalSeconds * backoffMultiplier);
                nextFromNotion = DateTime.UtcNow.AddSeconds(backoffSeconds);
            }
        }
    }

    private async Task<bool> RunFromNotionAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var syncService = scope.ServiceProvider.GetRequiredService<IFoodSyncService>();
            var summary = await syncService.SyncFromNotionAsync(cancellationToken);
            sw.Stop();

            _logger.LogInformation(
                "Food sync (Notion->DB) completed in {ElapsedMs} ms. Inventory={Inventory}, Meals={Meals}, Grocery={Grocery}, Ingredients={Ingredients}, Errors={Errors}",
                sw.ElapsedMilliseconds,
                summary.InventoryUpserted,
                summary.MealsUpserted,
                summary.GroceryItemsUpserted,
                summary.IngredientsLinked,
                summary.HasErrors ? summary.LastError : "none");

            return !summary.HasErrors;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Food sync (Notion->DB) failed after {ElapsedMs} ms", sw.ElapsedMilliseconds);
            return false;
        }
    }

    private async Task RunToNotionAsync(int batchSize, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var syncService = scope.ServiceProvider.GetRequiredService<IFoodSyncService>();
            var inventorySynced = await syncService.SyncInventoryChangesToNotionAsync(batchSize, cancellationToken);
            var grocerySynced = await syncService.SyncGroceryChangesToNotionAsync(batchSize, cancellationToken);

            if (inventorySynced > 0 || grocerySynced > 0)
            {
                _logger.LogInformation(
                    "Food sync (DB->Notion) pushed {InventoryCount} inventory changes and {GroceryCount} grocery changes",
                    inventorySynced,
                    grocerySynced);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Food sync (DB->Notion) run failed");
        }
    }
}
