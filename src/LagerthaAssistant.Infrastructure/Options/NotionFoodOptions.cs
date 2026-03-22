namespace LagerthaAssistant.Infrastructure.Options;

public sealed class NotionFoodOptions
{
    public bool Enabled { get; init; }

    public string ApiKey { get; init; } = string.Empty;

    public string InventoryDatabaseId { get; init; } = string.Empty;

    public string MealPlansDatabaseId { get; init; } = string.Empty;

    public string GroceryListDatabaseId { get; init; } = string.Empty;

    public string ApiBaseUrl { get; init; } = "https://api.notion.com/v1";

    public string Version { get; init; } = "2022-06-28";

    public int RequestTimeoutSeconds { get; init; } = 60;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(InventoryDatabaseId)
        && !string.IsNullOrWhiteSpace(MealPlansDatabaseId)
        && !string.IsNullOrWhiteSpace(GroceryListDatabaseId);
}
