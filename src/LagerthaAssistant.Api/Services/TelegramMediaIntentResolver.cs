using LagerthaAssistant.Api.Interfaces;

namespace LagerthaAssistant.Api.Services;

/// <summary>
/// Classifies inbound Telegram media (photo or document) into a <see cref="TelegramMediaKind"/>
/// and resolves the list of <see cref="TelegramMediaCapability"/> options that the user can choose from.
/// </summary>
public static class TelegramMediaIntentResolver
{
    private static readonly IReadOnlyList<TelegramMediaCapability> PhotoCapabilities =
        [TelegramMediaCapability.VocabImport, TelegramMediaCapability.InventoryRestock, TelegramMediaCapability.InventoryConsume, TelegramMediaCapability.FoodPhoto];

    private static readonly IReadOnlyList<TelegramMediaCapability> ImageDocumentCapabilities =
        [TelegramMediaCapability.VocabImport, TelegramMediaCapability.InventoryRestock, TelegramMediaCapability.InventoryConsume, TelegramMediaCapability.FoodPhoto];

    private static readonly IReadOnlyList<TelegramMediaCapability> TextDocumentCapabilities =
        [TelegramMediaCapability.VocabImport];

    private static readonly IReadOnlyList<TelegramMediaCapability> SpreadsheetCapabilities =
        [TelegramMediaCapability.VocabImport];

    private static readonly IReadOnlyList<TelegramMediaCapability> PdfCapabilities =
        [TelegramMediaCapability.VocabImport];

    private static readonly IReadOnlyList<TelegramMediaCapability> EmptyCapabilities = [];

    public static TelegramMediaKind ClassifyMedia(string? photoFileId, string? documentMimeType, string? documentFileName)
    {
        if (!string.IsNullOrWhiteSpace(photoFileId))
        {
            return TelegramMediaKind.Photo;
        }

        if (string.IsNullOrWhiteSpace(documentMimeType) && string.IsNullOrWhiteSpace(documentFileName))
        {
            return TelegramMediaKind.Unknown;
        }

        var mime = documentMimeType?.ToLowerInvariant() ?? string.Empty;
        var ext = Path.GetExtension(documentFileName ?? string.Empty).TrimStart('.').ToLowerInvariant();

        if (mime.StartsWith("image/", StringComparison.Ordinal)
            || ext is "jpg" or "jpeg" or "png" or "webp" or "gif" or "bmp" or "heic")
        {
            return TelegramMediaKind.ImageDocument;
        }

        if (mime is "application/pdf" || ext is "pdf")
        {
            return TelegramMediaKind.PdfDocument;
        }

        if (mime is "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                or "application/vnd.ms-excel"
                or "text/csv"
            || ext is "xlsx" or "xls" or "csv")
        {
            return TelegramMediaKind.Spreadsheet;
        }

        if (mime.StartsWith("text/", StringComparison.Ordinal)
            || mime is "application/msword"
                or "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            || ext is "txt" or "doc" or "docx" or "md" or "rtf")
        {
            return TelegramMediaKind.TextDocument;
        }

        return TelegramMediaKind.Unknown;
    }

    public static IReadOnlyList<TelegramMediaCapability> ResolveCapabilities(TelegramMediaKind kind)
    {
        return kind switch
        {
            TelegramMediaKind.Photo => PhotoCapabilities,
            TelegramMediaKind.ImageDocument => ImageDocumentCapabilities,
            TelegramMediaKind.TextDocument => TextDocumentCapabilities,
            TelegramMediaKind.Spreadsheet => SpreadsheetCapabilities,
            TelegramMediaKind.PdfDocument => PdfCapabilities,
            _ => EmptyCapabilities
        };
    }
}
