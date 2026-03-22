namespace LagerthaAssistant.Api.Options;

public sealed class FoodSyncWorkerOptions
{
    public bool Enabled { get; set; } = false;

    /// <summary>How often to pull from Notion (seconds).</summary>
    public int SyncFromNotionIntervalSeconds { get; set; } = 300;

    /// <summary>How often to push local changes to Notion (seconds).</summary>
    public int SyncToNotionIntervalSeconds { get; set; } = 30;

    public int BatchSize { get; set; } = 25;

    public bool RunOnStartup { get; set; } = true;

    public int MaxBackoffSeconds { get; set; } = 600;

    public int BackoffFactor { get; set; } = 2;
}
