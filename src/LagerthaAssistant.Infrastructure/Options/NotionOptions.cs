namespace LagerthaAssistant.Infrastructure.Options;

public sealed class NotionOptions
{
    public bool Enabled { get; init; }

    public string ApiKey { get; init; } = string.Empty;

    public string DatabaseId { get; init; } = string.Empty;

    public string ApiBaseUrl { get; init; } = "https://api.notion.com/v1";

    public string Version { get; init; } = "2022-06-28";

    public string ConflictMode { get; init; } = "update";

    public int RequestTimeoutSeconds { get; init; } = 60;

    public string KeyPropertyName { get; init; } = "Key";

    public string WordPropertyName { get; init; } = "Word";

    public string MeaningPropertyName { get; init; } = "Meaning";

    public string ExamplesPropertyName { get; init; } = "Examples";

    public string PartOfSpeechPropertyName { get; init; } = "PartOfSpeech";

    public string DeckPropertyName { get; init; } = "DeckFile";

    public string StorageModePropertyName { get; init; } = "StorageMode";

    public string RowNumberPropertyName { get; init; } = "RowNumber";

    public string LastSeenPropertyName { get; init; } = "LastSeenAtUtc";
}

