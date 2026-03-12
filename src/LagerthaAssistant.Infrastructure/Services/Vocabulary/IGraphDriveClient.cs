namespace LagerthaAssistant.Infrastructure.Services.Vocabulary;

public interface IGraphDriveClient
{
    Task<IReadOnlyList<GraphDriveFile>> ListFilesAsync(CancellationToken cancellationToken = default);

    Task<byte[]> DownloadFileContentAsync(string itemId, CancellationToken cancellationToken = default);

    Task<GraphUploadResult> UploadFileContentAsync(
        string itemId,
        byte[] content,
        string? expectedETag = null,
        CancellationToken cancellationToken = default);
}
