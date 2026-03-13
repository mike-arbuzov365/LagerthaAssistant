namespace LagerthaAssistant.Api.Options;

public sealed class NotionSyncWorkerOptions
{
    public bool Enabled { get; set; } = false;

    public int IntervalSeconds { get; set; } = 60;

    public int BatchSize { get; set; } = 25;

    public bool RunOnStartup { get; set; } = true;

    public int MaxBackoffSeconds { get; set; } = 300;

    public int BackoffFactor { get; set; } = 2;
}

