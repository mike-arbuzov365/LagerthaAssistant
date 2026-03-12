namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

public sealed record GraphUploadResult(
    bool Succeeded,
    string? Message = null,
    string? UpdatedETag = null);
