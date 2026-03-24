namespace LagerthaAssistant.Api.Controllers;

using LagerthaAssistant.Application.Interfaces.Food;
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

/// <param name="ArchivedCount">Number of orphaned Notion pages that were archived.</param>
/// <param name="GracePeriodMinutes">Grace period that was applied during the run.</param>
public sealed record FoodSyncReconcileGroceryResponse(int ArchivedCount, int GracePeriodMinutes);
