namespace BaguetteDesign.Domain.Entities;

public sealed class ClientFile : AuditableEntity
{
    public string ClientUserId { get; set; } = string.Empty;
    public int? ProjectId { get; set; }
    public string TelegramFileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;  // "text" | "reference" | "other"
    public string? MimeType { get; set; }
    public long FileSizeBytes { get; set; }
    public string? GoogleDriveFileId { get; set; }
    public string? GoogleDriveUrl { get; set; }
}
