namespace LagerthaAssistant.Api.Controllers;

using LagerthaAssistant.Application.Interfaces.Food;
using LagerthaAssistant.Application.Models.Food;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/food-sync")]
public sealed class FoodSyncController : ControllerBase
{
    private readonly IFoodSyncService _foodSyncService;

    public FoodSyncController(IFoodSyncService foodSyncService)
    {
        _foodSyncService = foodSyncService;
    }

    /// <summary>
    /// Returns a snapshot of the food sync queue health (pending, failed, permanently-failed counts).
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(FoodSyncStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FoodSyncStatusResponse>> GetStatus(CancellationToken cancellationToken = default)
    {
        var summary = await _foodSyncService.GetSyncStatusAsync(cancellationToken);
        return Ok(new FoodSyncStatusResponse(
            summary.InventoryPendingOrFailed,
            summary.InventoryPermanentlyFailed,
            summary.GroceryPendingOrFailed,
            summary.GroceryPermanentlyFailed));
    }

    /// <summary>
    /// Archives active Notion Grocery List pages that have no corresponding local record.
    /// Use this to clean up items that were deleted locally but never archived in Notion.
    /// </summary>
    /// <param name="gracePeriodMinutes">
    /// Items edited within this many minutes are skipped (default: 60).
    /// Increase when you know no local↔Notion operations are in-flight.
    /// </param>
    [HttpPost("reconcile-grocery")]
    [ProducesResponseType(typeof(FoodSyncReconcileGroceryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FoodSyncReconcileGroceryResponse>> ReconcileGrocery(
        [FromQuery] int gracePeriodMinutes = 60,
        CancellationToken cancellationToken = default)
    {
        if (gracePeriodMinutes < 0)
        {
            return BadRequest("Parameter 'gracePeriodMinutes' must be non-negative.");
        }

        var archived = await _foodSyncService.ReconcileNotionGroceryOrphansAsync(
            TimeSpan.FromMinutes(gracePeriodMinutes),
            cancellationToken);

        return Ok(new FoodSyncReconcileGroceryResponse(archived, gracePeriodMinutes));
    }
}

/// <param name="InventoryPendingOrFailed">Inventory items queued for retry.</param>
/// <param name="InventoryPermanentlyFailed">Inventory items that exceeded max retry attempts.</param>
/// <param name="GroceryPendingOrFailed">Grocery items queued for retry.</param>
/// <param name="GroceryPermanentlyFailed">Grocery items that exceeded max retry attempts.</param>
public sealed record FoodSyncStatusResponse(
    int InventoryPendingOrFailed,
    int InventoryPermanentlyFailed,
    int GroceryPendingOrFailed,
    int GroceryPermanentlyFailed);

/// <param name="ArchivedCount">Number of orphaned Notion pages that were archived.</param>
/// <param name="GracePeriodMinutes">Grace period that was applied during the run.</param>
public sealed record FoodSyncReconcileGroceryResponse(int ArchivedCount, int GracePeriodMinutes);
