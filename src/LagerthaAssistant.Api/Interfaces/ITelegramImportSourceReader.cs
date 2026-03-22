namespace LagerthaAssistant.Api.Interfaces;

public interface ITelegramImportSourceReader
{
    Task<TelegramImportSourceReadResult> ReadTextAsync(
        TelegramImportInbound inbound,
        TelegramImportSourceType sourceType,
        CancellationToken cancellationToken = default);

    Task<TelegramFoodIdentificationResult> IdentifyFoodAsync(
        string photoFileId,
        CancellationToken cancellationToken = default);
}

public sealed record TelegramFoodIdentificationResult(
    bool Success,
    string? MealName = null,
    int? EstimatedCalories = null,
    string? Error = null);

public sealed record TelegramImportInbound(
    string Text,
    string? DocumentFileId,
    string? DocumentFileName,
    string? DocumentMimeType,
    string? PhotoFileId);

public enum TelegramImportSourceType
{
    Url = 0,
    Text = 1,
    File = 2,
    Photo = 3
}

public enum TelegramImportSourceReadStatus
{
    Success = 0,
    WrongInputType = 1,
    InvalidSource = 2,
    UnsupportedFileType = 3,
    NoTextExtracted = 4,
    Failed = 5
}

public sealed record TelegramImportSourceReadResult(
    TelegramImportSourceReadStatus Status,
    string? Text = null,
    string? Error = null);
