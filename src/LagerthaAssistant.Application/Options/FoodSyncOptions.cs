namespace LagerthaAssistant.Application.Options;

public sealed class FoodSyncOptions
{
    /// <summary>
    /// Maximum number of consecutive sync attempts before an item is marked PermanentlyFailed.
    /// Prevents runaway retry loops for items with persistent Notion API errors.
    /// </summary>
    public int MaxSyncAttempts { get; init; } = 5;
}
