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

    Task<TelegramInventoryPhotoAnalysisResult> AnalyzeInventoryPhotoAsync(
        string photoFileId,
        TelegramInventoryPhotoMode mode,
        IReadOnlyList<TelegramInventoryItemHint> inventoryItems,
        CancellationToken cancellationToken = default);
}

public sealed record TelegramFoodIdentificationResult(
    bool Success,
    string? MealName = null,
    int? EstimatedCalories = null,
    string? Error = null);

public enum TelegramInventoryPhotoMode
{
    Restock = 0,
    Consumption = 1
}

public sealed record TelegramInventoryItemHint(
    int Id,
    string Name);

public sealed record TelegramInventoryPhotoCandidate(
    int ItemId,
    string Name,
    decimal Quantity,
    string? Unit,
    double Confidence,
    decimal? PriceTotal = null,
    decimal? PricePerUnit = null);

public sealed record TelegramInventoryPhotoUnknown(
    string Name,
    string? NameEn,
    decimal Quantity,
    string? Unit,
    double Confidence,
    decimal? PriceTotal = null,
    decimal? PricePerUnit = null,
    bool IsNonProduct = false);

public sealed record TelegramInventoryPhotoDetectedStore(
    string Name,
    string? NameEn,
    double Confidence);

public sealed record TelegramInventoryPhotoAnalysisResult(
    bool Success,
    IReadOnlyList<TelegramInventoryPhotoCandidate> Candidates,
    IReadOnlyList<TelegramInventoryPhotoUnknown> Unknown,
    TelegramInventoryPhotoDetectedStore? DetectedStore = null,
    IReadOnlyList<string>? NonProducts = null,
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

public enum TelegramMediaKind
{
    Photo = 0,
    ImageDocument = 1,
    TextDocument = 2,
    PdfDocument = 3,
    Spreadsheet = 4,
    Unknown = 5
}

public enum TelegramMediaCapability
{
    VocabImport = 0,
    InventoryRestock = 1,
    InventoryConsume = 2,
    FoodPhoto = 3
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
