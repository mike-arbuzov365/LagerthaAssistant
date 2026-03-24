namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Application.Interfaces.Food;
using LagerthaAssistant.Application.Models.Food;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class FoodSyncControllerTests
{
    // ── ReconcileGrocery ─────────────────────────────────────────────────────

    [Fact]
    public async Task ReconcileGrocery_ShouldReturnOk_WithArchivedCount()
    {
        var service = new FakeFoodSyncService { ReconcileResult = 3 };
        var sut = new FoodSyncController(service);

        var result = await sut.ReconcileGrocery(gracePeriodMinutes: 60, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<FoodSyncReconcileGroceryResponse>(ok.Value);
        Assert.Equal(3, payload.ArchivedCount);
        Assert.Equal(60, payload.GracePeriodMinutes);
    }

    [Fact]
    public async Task ReconcileGrocery_ShouldReturnBadRequest_WhenGracePeriodIsNegative()
    {
        var service = new FakeFoodSyncService();
        var sut = new FoodSyncController(service);

        var result = await sut.ReconcileGrocery(gracePeriodMinutes: -1, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task ReconcileGrocery_ShouldPassGracePeriodToService()
    {
        var service = new FakeFoodSyncService();
        var sut = new FoodSyncController(service);

        await sut.ReconcileGrocery(gracePeriodMinutes: 30, CancellationToken.None);

        Assert.Equal(TimeSpan.FromMinutes(30), service.LastGracePeriod);
    }

    [Fact]
    public async Task ReconcileGrocery_ShouldAcceptZeroGracePeriod()
    {
        var service = new FakeFoodSyncService { ReconcileResult = 5 };
        var sut = new FoodSyncController(service);

        var result = await sut.ReconcileGrocery(gracePeriodMinutes: 0, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<FoodSyncReconcileGroceryResponse>(ok.Value);
        Assert.Equal(5, payload.ArchivedCount);
        Assert.Equal(0, payload.GracePeriodMinutes);
    }

    // ── Fake ─────────────────────────────────────────────────────────────────

    private sealed class FakeFoodSyncService : IFoodSyncService
    {
        public int ReconcileResult { get; init; }
        public TimeSpan? LastGracePeriod { get; private set; }

        public Task<FoodSyncSummary> SyncFromNotionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new FoodSyncSummary(0, 0, 0, 0, false, null));

        public Task<int> SyncGroceryChangesToNotionAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> SyncInventoryChangesToNotionAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> ReconcileNotionGroceryOrphansAsync(TimeSpan? gracePeriod = null, CancellationToken cancellationToken = default)
        {
            LastGracePeriod = gracePeriod;
            return Task.FromResult(ReconcileResult);
        }
    }
}
