namespace LagerthaAssistant.Api.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Application.Interfaces.Food;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Infrastructure.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/integrations")]
public sealed class IntegrationsController : ControllerBase
{
    private readonly INotionSyncProcessor _notionSyncProcessor;
    private readonly IFoodSyncService _foodSyncService;
    private readonly NotionFoodOptions _notionFoodOptions;
    private readonly NotionSyncWorkerOptions _notionSyncWorkerOptions;
    private readonly FoodSyncWorkerOptions _foodSyncWorkerOptions;

    public IntegrationsController(
        INotionSyncProcessor notionSyncProcessor,
        IFoodSyncService foodSyncService,
        NotionFoodOptions notionFoodOptions,
        IOptions<NotionSyncWorkerOptions> notionSyncWorkerOptions,
        IOptions<FoodSyncWorkerOptions> foodSyncWorkerOptions)
    {
        _notionSyncProcessor = notionSyncProcessor;
        _foodSyncService = foodSyncService;
        _notionFoodOptions = notionFoodOptions;
        _notionSyncWorkerOptions = notionSyncWorkerOptions.Value;
        _foodSyncWorkerOptions = foodSyncWorkerOptions.Value;
    }

    [HttpGet("notion/status")]
    [ProducesResponseType(typeof(IntegrationNotionHubStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<IntegrationNotionHubStatusResponse>> GetNotionStatus(
        CancellationToken cancellationToken = default)
    {
        var notionStatus = await _notionSyncProcessor.GetStatusAsync(cancellationToken);
        var foodStatus = await _foodSyncService.GetSyncStatusAsync(cancellationToken);

        return Ok(new IntegrationNotionHubStatusResponse(
            NotionVocabulary: new IntegrationNotionStatusResponse(
                Enabled: notionStatus.Enabled,
                IsConfigured: notionStatus.IsConfigured,
                WorkerEnabled: _notionSyncWorkerOptions.Enabled,
                Message: notionStatus.Message,
                PendingCards: notionStatus.PendingCards,
                FailedCards: notionStatus.FailedCards),
            NotionFood: new IntegrationFoodStatusResponse(
                Enabled: _notionFoodOptions.Enabled,
                IsConfigured: _notionFoodOptions.IsConfigured,
                WorkerEnabled: _foodSyncWorkerOptions.Enabled,
                InventoryPendingOrFailed: foodStatus.InventoryPendingOrFailed,
                InventoryPermanentlyFailed: foodStatus.InventoryPermanentlyFailed,
                GroceryPendingOrFailed: foodStatus.GroceryPendingOrFailed,
                GroceryPermanentlyFailed: foodStatus.GroceryPermanentlyFailed)));
    }
}
