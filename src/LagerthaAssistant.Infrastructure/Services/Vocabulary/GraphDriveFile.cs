namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

public sealed record GraphDriveFile(
    string Id,
    string Name,
    string ETag,
    string FullPath);
