namespace LagerthaAssistant.Domain.Enums;

public enum FoodSyncStatus
{
    Pending,
    Processing,
    Synced,
    Failed,

    /// <summary>
    /// Sync has failed more than <c>MaxSyncAttempts</c> times.
    /// The item will not be retried automatically; manual intervention is required.
    /// </summary>
    PermanentlyFailed
}
