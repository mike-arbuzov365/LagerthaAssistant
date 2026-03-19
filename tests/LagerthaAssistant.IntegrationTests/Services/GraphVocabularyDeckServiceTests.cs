namespace LagerthaAssistant.IntegrationTests.Services;

using System.IO.Compression;
using System.Xml.Linq;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;
using LagerthaAssistant.Infrastructure.Options;
using LagerthaAssistant.Infrastructure.Services.Vocabulary;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class GraphVocabularyDeckServiceTests
{
    [Fact]
    public async Task DisposeAsync_CalledTwice_ShouldNotThrow()
    {
        var workbookBytes = CreateTemplateWorkbookBytes("build", "(v) to build software components", "We build services daily.");
        var remoteFile = new GraphDriveFile("file-1", "wm-verbs-us-en.xlsx", "etag-1", "/Apps/Flashcards Deluxe/wm-verbs-us-en.xlsx");

        var graphClient = new FakeGraphDriveClient(
            [remoteFile],
            new Dictionary<string, byte[]> { [remoteFile.Id] = workbookBytes },
            [new GraphUploadResult(true, UpdatedETag: "etag-2")]);

        var sut = CreateSut(graphClient);

        await sut.DisposeAsync();
        await sut.DisposeAsync();
    }

    [Fact]
    public async Task PreviewThenAppend_ShouldReuseSessionMirror_AndAvoidRedownload()
    {
        var workbookBytes = CreateTemplateWorkbookBytes("build", "(v) to build software components", "We build services daily.");
        var remoteFile = new GraphDriveFile("file-1", "wm-verbs-us-en.xlsx", "etag-1", "/Apps/Flashcards Deluxe/wm-verbs-us-en.xlsx");

        var graphClient = new FakeGraphDriveClient(
            [remoteFile],
            new Dictionary<string, byte[]> { [remoteFile.Id] = workbookBytes },
            [new GraphUploadResult(true, UpdatedETag: "etag-2")]);

        await using var sut = CreateSut(graphClient);

        const string reply = """
void

(v) no return value

The function returns void when there is no value to return.
""";

        var preview = await sut.PreviewAppendFromAssistantReplyAsync("void", reply);
        Assert.Equal(VocabularyAppendPreviewStatus.ReadyToAppend, preview.Status);

        var append = await sut.AppendFromAssistantReplyAsync("void", reply);
        Assert.Equal(VocabularyAppendStatus.Added, append.Status);

        Assert.Equal(1, graphClient.ListFilesCalls);
        Assert.Equal(1, graphClient.DownloadCalls);
        Assert.Equal(1, graphClient.UploadCalls);
    }

    [Fact]
    public async Task FindInWritableDecksAsync_MultipleCalls_ShouldReuseSessionMirror_AndAvoidRedownload()
    {
        var workbookBytes = CreateTemplateWorkbookBytes("build", "(v) to build software components", "We build services daily.");
        var remoteFile = new GraphDriveFile("file-1", "wm-verbs-us-en.xlsx", "etag-1", "/Apps/Flashcards Deluxe/wm-verbs-us-en.xlsx");

        var graphClient = new FakeGraphDriveClient(
            [remoteFile],
            new Dictionary<string, byte[]> { [remoteFile.Id] = workbookBytes },
            [new GraphUploadResult(true, UpdatedETag: "etag-2")]);

        await using var sut = CreateSut(graphClient);

        var firstLookup = await sut.FindInWritableDecksAsync("build");
        var secondLookup = await sut.FindInWritableDecksAsync("build");

        Assert.True(firstLookup.Found);
        Assert.True(secondLookup.Found);
        Assert.Equal(1, graphClient.ListFilesCalls);
        Assert.Equal(1, graphClient.DownloadCalls);
        Assert.Equal(0, graphClient.UploadCalls);
    }

    [Fact]
    public async Task FindInWritableDecksBatchAsync_RepeatedLargeBatches_ShouldUseSingleMirrorSetup()
    {
        var workbookBytes = CreateTemplateWorkbookBytes("build", "(v) to build software components", "We build services daily.");
        var remoteFile = new GraphDriveFile("file-1", "wm-verbs-us-en.xlsx", "etag-1", "/Apps/Flashcards Deluxe/wm-verbs-us-en.xlsx");

        var graphClient = new FakeGraphDriveClient(
            [remoteFile],
            new Dictionary<string, byte[]> { [remoteFile.Id] = workbookBytes },
            [new GraphUploadResult(true, UpdatedETag: "etag-2")]);

        await using var sut = CreateSut(graphClient);

        var firstBatch = Enumerable.Range(0, 120).Select(index => $"word-{index}").ToList();
        firstBatch[10] = "build";
        firstBatch[90] = "build";

        var secondBatch = Enumerable.Range(0, 120).Select(index => $"next-{index}").ToList();
        secondBatch[35] = "build";

        var firstLookup = await sut.FindInWritableDecksBatchAsync(firstBatch);
        var secondLookup = await sut.FindInWritableDecksBatchAsync(secondBatch);

        Assert.True(firstLookup["build"].Found);
        Assert.True(secondLookup["build"].Found);
        Assert.Equal(1, graphClient.ListFilesCalls);
        Assert.Equal(1, graphClient.DownloadCalls);
        Assert.Equal(0, graphClient.UploadCalls);
    }

    [Fact]
    public async Task GetWritableDeckFilesAsync_WhenMirrorNotInitialized_ShouldListRemoteFilesWithoutDownload()
    {
        var workbookBytes = CreateTemplateWorkbookBytes("build", "(v) to build software components", "We build services daily.");
        var remoteFile = new GraphDriveFile("file-1", "wm-verbs-us-en.xlsx", "etag-1", "/Apps/Flashcards Deluxe/wm-verbs-us-en.xlsx");

        var graphClient = new FakeGraphDriveClient(
            [remoteFile],
            new Dictionary<string, byte[]> { [remoteFile.Id] = workbookBytes },
            []);

        await using var sut = CreateSut(graphClient);

        var decks = await sut.GetWritableDeckFilesAsync();

        var deck = Assert.Single(decks);
        Assert.Equal("wm-verbs-us-en.xlsx", deck.FileName);
        Assert.Equal("/Apps/Flashcards Deluxe/wm-verbs-us-en.xlsx", deck.FullPath);
        Assert.Equal(1, graphClient.ListFilesCalls);
        Assert.Equal(0, graphClient.DownloadCalls);
    }

    [Fact]
    public async Task AppendRetryAfterLock_ShouldRetryPendingUpload_WithoutDuplicateLocalAppend()
    {
        var workbookBytes = CreateTemplateWorkbookBytes("build", "(v) to build software components", "We build services daily.");
        var remoteFile = new GraphDriveFile("file-1", "wm-verbs-us-en.xlsx", "etag-1", "/Apps/Flashcards Deluxe/wm-verbs-us-en.xlsx");

        var graphClient = new FakeGraphDriveClient(
            [remoteFile],
            new Dictionary<string, byte[]> { [remoteFile.Id] = workbookBytes },
            [
                new GraphUploadResult(false, "OneDrive file is locked right now. Close it in other apps and retry."),
                new GraphUploadResult(true, UpdatedETag: "etag-2")
            ]);

        await using var sut = CreateSut(graphClient);

        const string reply = """
void

(v) no return value

The function returns void when there is no value to return.
""";

        var preview = await sut.PreviewAppendFromAssistantReplyAsync("void", reply);
        Assert.Equal(VocabularyAppendPreviewStatus.ReadyToAppend, preview.Status);

        var firstAppend = await sut.AppendFromAssistantReplyAsync("void", reply);
        Assert.Equal(VocabularyAppendStatus.Error, firstAppend.Status);
        Assert.Contains("locked", firstAppend.Message, StringComparison.OrdinalIgnoreCase);

        var secondAppend = await sut.AppendFromAssistantReplyAsync("void", reply);
        Assert.Equal(VocabularyAppendStatus.Added, secondAppend.Status);

        Assert.Equal(2, graphClient.UploadCalls);

        var storedBytes = graphClient.GetStoredContent(remoteFile.Id);
        Assert.NotNull(storedBytes);
        Assert.Equal(1, CountWordOccurrencesInColumnB(storedBytes!, "void"));
    }

    private static GraphVocabularyDeckService CreateSut(FakeGraphDriveClient graphClient)
    {
        var options = new VocabularyDeckOptions
        {
            FilePattern = "wm-*.xlsx",
            ReadOnlyFileNames = [],
            VerbDeckFileName = "wm-verbs-us-en.xlsx"
        };

        return new GraphVocabularyDeckService(
            options,
            new VocabularyReplyParser(),
            graphClient,
            NullLoggerFactory.Instance,
            NullLogger<GraphVocabularyDeckService>.Instance);
    }

    private static int CountWordOccurrencesInColumnB(byte[] workbookBytes, string word)
    {
        using var stream = new MemoryStream(workbookBytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml")
            ?? throw new InvalidOperationException("Missing worksheet entry.");

        using var reader = new StreamReader(sheetEntry.Open());
        var sheetXml = XDocument.Parse(reader.ReadToEnd());

        var ns = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");

        return sheetXml
            .Descendants(ns + "c")
            .Where(cell =>
            {
                var reference = cell.Attribute("r")?.Value;
                return !string.IsNullOrWhiteSpace(reference)
                    && reference.StartsWith("B", StringComparison.OrdinalIgnoreCase);
            })
            .Count(cell => string.Equals(ReadInlineString(cell, ns), word, StringComparison.OrdinalIgnoreCase));
    }

    private static string ReadInlineString(XElement cell, XNamespace ns)
    {
        var inline = cell.Element(ns + "is");
        if (inline is null)
        {
            return string.Empty;
        }

        var directText = inline.Element(ns + "t");
        if (directText is not null)
        {
            return directText.Value;
        }

        return string.Concat(inline
            .Elements(ns + "r")
            .Select(run => run.Element(ns + "t")?.Value ?? string.Empty));
    }

    private static byte[] CreateTemplateWorkbookBytes(string initialWord, string initialMeaning, string initialExamples)
    {
        using var stream = new MemoryStream();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);

        WriteEntry(archive, "[Content_Types].xml", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
  <Default Extension="xml" ContentType="application/xml" />
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" />
  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" />
</Types>
""");

        WriteEntry(archive, "_rels/.rels", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" />
</Relationships>
""");

        WriteEntry(archive, "xl/workbook.xml", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets>
    <sheet name="Words" sheetId="1" r:id="rId1" />
  </sheets>
</workbook>
""");

        WriteEntry(archive, "xl/_rels/workbook.xml.rels", """
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" />
</Relationships>
""");

        WriteEntry(archive, "xl/worksheets/sheet1.xml", $"""
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
  <sheetData>
    <row r="10">
      <c r="A10" t="inlineStr"><is><t>Translation</t></is></c>
      <c r="B10" t="inlineStr"><is><t>Word</t></is></c>
      <c r="H10" t="inlineStr"><is><t>Examples</t></is></c>
    </row>
    <row r="11">
      <c r="A11" t="inlineStr"><is><t xml:space="preserve">{EscapeXml(initialMeaning)}</t></is></c>
      <c r="B11" t="inlineStr"><is><t xml:space="preserve">{EscapeXml(initialWord)}</t></is></c>
      <c r="H11" t="inlineStr"><is><t xml:space="preserve">{EscapeXml(initialExamples)}</t></is></c>
    </row>
  </sheetData>
</worksheet>
""");

        archive.Dispose();
        return stream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string entryPath, string content)
    {
        var entry = archive.CreateEntry(entryPath, CompressionLevel.NoCompression);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream);
        writer.Write(content);
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }

    private sealed class FakeGraphDriveClient : IGraphDriveClient
    {
        private readonly List<GraphDriveFile> _files;
        private readonly Dictionary<string, byte[]> _contentsById;
        private readonly Queue<GraphUploadResult> _uploadResults;

        public FakeGraphDriveClient(
            IReadOnlyList<GraphDriveFile> files,
            Dictionary<string, byte[]> contentsById,
            IReadOnlyList<GraphUploadResult> uploadResults)
        {
            _files = files.ToList();
            _contentsById = contentsById;
            _uploadResults = new Queue<GraphUploadResult>(uploadResults);
        }

        public int ListFilesCalls { get; private set; }

        public int DownloadCalls { get; private set; }

        public int UploadCalls { get; private set; }

        public Task<IReadOnlyList<GraphDriveFile>> ListFilesAsync(CancellationToken cancellationToken = default)
        {
            ListFilesCalls++;
            return Task.FromResult<IReadOnlyList<GraphDriveFile>>(_files.ToList());
        }

        public Task<byte[]> DownloadFileContentAsync(string itemId, CancellationToken cancellationToken = default)
        {
            DownloadCalls++;

            if (!_contentsById.TryGetValue(itemId, out var bytes))
            {
                throw new InvalidOperationException($"Unknown file id '{itemId}'.");
            }

            return Task.FromResult(bytes.ToArray());
        }

        public Task<GraphUploadResult> UploadFileContentAsync(
            string itemId,
            byte[] content,
            string? expectedETag = null,
            CancellationToken cancellationToken = default)
        {
            UploadCalls++;

            var result = _uploadResults.Count > 0
                ? _uploadResults.Dequeue()
                : new GraphUploadResult(true, UpdatedETag: "etag-auto");

            if (!result.Succeeded)
            {
                return Task.FromResult(result);
            }

            _contentsById[itemId] = content.ToArray();

            var fileIndex = _files.FindIndex(x => x.Id == itemId);
            if (fileIndex >= 0)
            {
                var currentFile = _files[fileIndex];
                var nextETag = string.IsNullOrWhiteSpace(result.UpdatedETag)
                    ? currentFile.ETag
                    : result.UpdatedETag;

                _files[fileIndex] = currentFile with { ETag = nextETag };
            }

            return Task.FromResult(result);
        }

        public byte[]? GetStoredContent(string itemId)
        {
            return _contentsById.TryGetValue(itemId, out var bytes)
                ? bytes.ToArray()
                : null;
        }
    }
}


