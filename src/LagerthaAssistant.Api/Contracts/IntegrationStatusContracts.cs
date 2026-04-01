namespace LagerthaAssistant.Api.Contracts;

public sealed record IntegrationNotionStatusResponse(
    bool Enabled,
    bool IsConfigured,
    bool WorkerEnabled,
    string Message,
    int PendingCards,
    int FailedCards);

public sealed record IntegrationFoodStatusResponse(
    bool Enabled,
    bool IsConfigured,
    bool WorkerEnabled,
    int InventoryPendingOrFailed,
    int InventoryPermanentlyFailed,
    int GroceryPendingOrFailed,
    int GroceryPermanentlyFailed);

public sealed record IntegrationNotionHubStatusResponse(
    IntegrationNotionStatusResponse NotionVocabulary,
    IntegrationFoodStatusResponse NotionFood);
