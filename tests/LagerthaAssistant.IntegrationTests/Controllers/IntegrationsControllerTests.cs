namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Application.Interfaces.Food;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Food;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Infrastructure.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class IntegrationsControllerTests
{
    [Fact]
    public async Task GetNotionStatus_ShouldReturnCombinedHubStatus()
    {
        var notionProcessor = new FakeNotionSyncProcessor
        {
            Status = new NotionSyncStatusSummary(
                Enabled: true,
                IsConfigured: true,
                Message: "Configured",
                PendingCards: 4,
                FailedCards: 1)
        };
        var foodSyncService = new FakeFoodSyncService
        {
            Status = new FoodSyncStatusSummary(
                InventoryPendingOrFailed: 3,
                InventoryPermanentlyFailed: 1,
                GroceryPendingOrFailed: 2,
                GroceryPermanentlyFailed: 0)
        };

        var sut = new IntegrationsController(
            notionProcessor,
            foodSyncService,
            new NotionFoodOptions
            {
                Enabled = true,
                ApiKey = "key",
                InventoryDatabaseId = "inv",
                MealPlansDatabaseId = "meal",
                GroceryListDatabaseId = "gro"
            },
            Options.Create(new NotionSyncWorkerOptions { Enabled = true }),
            Options.Create(new FoodSyncWorkerOptions { Enabled = true }));

        var response = await sut.GetNotionStatus(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<IntegrationNotionHubStatusResponse>(ok.Value);

        Assert.True(payload.NotionVocabulary.Enabled);
        Assert.True(payload.NotionVocabulary.IsConfigured);
        Assert.True(payload.NotionVocabulary.WorkerEnabled);
        Assert.Equal(4, payload.NotionVocabulary.PendingCards);
        Assert.Equal(1, payload.NotionVocabulary.FailedCards);

        Assert.True(payload.NotionFood.Enabled);
        Assert.True(payload.NotionFood.IsConfigured);
        Assert.True(payload.NotionFood.WorkerEnabled);
        Assert.Equal(3, payload.NotionFood.InventoryPendingOrFailed);
        Assert.Equal(2, payload.NotionFood.GroceryPendingOrFailed);
    }

    private sealed class FakeNotionSyncProcessor : INotionSyncProcessor
    {
        public NotionSyncStatusSummary Status { get; set; } = new(
            Enabled: false,
            IsConfigured: false,
            Message: "Disabled",
            PendingCards: 0,
            FailedCards: 0);

        public Task<NotionSyncStatusSummary> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Status);

        public Task<NotionSyncRunSummary> ProcessPendingAsync(int take, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<NotionSyncFailedCard>> GetFailedCardsAsync(int take, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> RequeueFailedAsync(int take, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeFoodSyncService : IFoodSyncService
    {
        public FoodSyncStatusSummary Status { get; set; } = new(0, 0, 0, 0);

        public Task<FoodSyncSummary> SyncFromNotionAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> SyncGroceryChangesToNotionAsync(int take, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> SyncInventoryChangesToNotionAsync(int take, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<int> ReconcileNotionGroceryOrphansAsync(TimeSpan? gracePeriod = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<FoodSyncStatusSummary> GetSyncStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Status);

        public Task<int> PurgeArchivedGroceryAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
