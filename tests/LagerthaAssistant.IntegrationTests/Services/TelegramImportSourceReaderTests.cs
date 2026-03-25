namespace LagerthaAssistant.IntegrationTests.Services;

using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Infrastructure.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class TelegramImportSourceReaderTests
{
    [Fact]
    public async Task ReadTextAsync_ShouldExtractText_FromExcelFile()
    {
        var xlsxBytes = CreateSimpleWorkbookBytes("alpha", "beta");
        using var handler = new StubTelegramFileHttpMessageHandler();
        handler.AddFile("xlsx-id", "docs/import.xlsx", xlsxBytes);

        var sut = CreateSut(handler);
        var inbound = new TelegramImportInbound(
            Text: string.Empty,
            DocumentFileId: "xlsx-id",
            DocumentFileName: "import.xlsx",
            DocumentMimeType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            PhotoFileId: null);

        var result = await sut.ReadTextAsync(inbound, TelegramImportSourceType.File, CancellationToken.None);

        Assert.Equal(TelegramImportSourceReadStatus.Success, result.Status);
        Assert.Contains("alpha", result.Text ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("beta", result.Text ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadTextAsync_ShouldTreatPdfAsSupportedFileType()
    {
        using var handler = new StubTelegramFileHttpMessageHandler();
        handler.AddFile("pdf-id", "docs/import.pdf", Encoding.UTF8.GetBytes("not-a-real-pdf"));

        var sut = CreateSut(handler);
        var inbound = new TelegramImportInbound(
            Text: string.Empty,
            DocumentFileId: "pdf-id",
            DocumentFileName: "import.pdf",
            DocumentMimeType: "application/pdf",
            PhotoFileId: null);

        var result = await sut.ReadTextAsync(inbound, TelegramImportSourceType.File, CancellationToken.None);

        Assert.NotEqual(TelegramImportSourceReadStatus.UnsupportedFileType, result.Status);
        Assert.True(
            result.Status is TelegramImportSourceReadStatus.Failed or TelegramImportSourceReadStatus.NoTextExtracted,
            $"Unexpected status: {result.Status}");
    }

    [Fact]
    public async Task ReadTextAsync_ShouldExtractText_FromWordDocxFile()
    {
        var docxBytes = CreateSimpleDocxBytes("First paragraph", "Second paragraph");
        using var handler = new StubTelegramFileHttpMessageHandler();
        handler.AddFile("docx-id", "docs/import.docx", docxBytes);

        var sut = CreateSut(handler);
        var inbound = new TelegramImportInbound(
            Text: string.Empty,
            DocumentFileId: "docx-id",
            DocumentFileName: "import.docx",
            DocumentMimeType: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            PhotoFileId: null);

        var result = await sut.ReadTextAsync(inbound, TelegramImportSourceType.File, CancellationToken.None);

        Assert.Equal(TelegramImportSourceReadStatus.Success, result.Status);
        Assert.Contains("First paragraph", result.Text ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("Second paragraph", result.Text ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeInventoryPhotoAsync_ShouldFail_WhenInventoryIsEmpty()
    {
        using var handler = new StubTelegramFileHttpMessageHandler();
        var sut = CreateSut(handler);

        var result = await sut.AnalyzeInventoryPhotoAsync(
            photoFileId: "photo-id",
            mode: TelegramInventoryPhotoMode.Restock,
            inventoryItems: [],
            cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(result.Candidates);
        Assert.Empty(result.Unknown);
        Assert.Contains("Inventory is empty", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeInventoryPhotoAsync_ShouldFail_WhenOpenAiApiKeyIsMissing()
    {
        using var handler = new StubTelegramFileHttpMessageHandler();
        handler.AddFile("photo-id", "photos/inventory.jpg", [1, 2, 3, 4]);

        var sut = CreateSut(handler);

        var result = await sut.AnalyzeInventoryPhotoAsync(
            photoFileId: "photo-id",
            mode: TelegramInventoryPhotoMode.Restock,
            inventoryItems:
            [
                new TelegramInventoryItemHint(1, "Milk")
            ],
            cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(result.Candidates);
        Assert.Empty(result.Unknown);
        Assert.Contains("API key", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    // ── ParseDetectedStore ─────────────────────────────────────────────────

    [Fact]
    public async Task AnalyzeInventoryPhotoAsync_ShouldParseDetectedStore_FromJson()
    {
        using var handler = new StubTelegramFileHttpMessageHandler();
        handler.AddFile("photo-id", "photos/receipt.jpg", [1, 2, 3, 4]);
        handler.OpenAiResponseJson = """{"store":{"name":"АТБ","nameEn":"ATB","confidence":0.92},"candidates":[],"unknown":[]}""";

        var sut = CreateSutWithOpenAi(handler);

        var result = await sut.AnalyzeInventoryPhotoAsync(
            photoFileId: "photo-id",
            mode: TelegramInventoryPhotoMode.Restock,
            inventoryItems: [new TelegramInventoryItemHint(1, "Milk")],
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.DetectedStore);
        Assert.Equal("АТБ", result.DetectedStore.Name);
        Assert.Equal("ATB", result.DetectedStore.NameEn);
        Assert.Equal(0.92, result.DetectedStore.Confidence, 2);
    }

    [Fact]
    public async Task AnalyzeInventoryPhotoAsync_ShouldReturnNullStore_WhenNoStoreField()
    {
        using var handler = new StubTelegramFileHttpMessageHandler();
        handler.AddFile("photo-id", "photos/receipt.jpg", [1, 2, 3, 4]);
        handler.OpenAiResponseJson = """{"candidates":[],"unknown":[]}""";

        var sut = CreateSutWithOpenAi(handler);

        var result = await sut.AnalyzeInventoryPhotoAsync(
            photoFileId: "photo-id",
            mode: TelegramInventoryPhotoMode.Restock,
            inventoryItems: [new TelegramInventoryItemHint(1, "Milk")],
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.DetectedStore);
    }

    [Fact]
    public async Task AnalyzeInventoryPhotoAsync_ShouldReturnNullStore_WhenStoreNameIsEmpty()
    {
        using var handler = new StubTelegramFileHttpMessageHandler();
        handler.AddFile("photo-id", "photos/receipt.jpg", [1, 2, 3, 4]);
        handler.OpenAiResponseJson = """{"store":{"name":"","nameEn":"","confidence":0.5},"candidates":[],"unknown":[]}""";

        var sut = CreateSutWithOpenAi(handler);

        var result = await sut.AnalyzeInventoryPhotoAsync(
            photoFileId: "photo-id",
            mode: TelegramInventoryPhotoMode.Restock,
            inventoryItems: [new TelegramInventoryItemHint(1, "Milk")],
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.DetectedStore);
    }

    // ── Price fields on candidates and unknown ───────────────────────────────

    [Fact]
    public async Task AnalyzeInventoryPhotoAsync_ShouldParsePrices_OnCandidates()
    {
        using var handler = new StubTelegramFileHttpMessageHandler();
        handler.AddFile("photo-id", "photos/receipt.jpg", [1, 2, 3, 4]);
        handler.OpenAiResponseJson = """{"candidates":[{"itemId":1,"quantity":2,"unit":"pcs","confidence":0.95,"priceTotal":189.50,"pricePerUnit":94.75}],"unknown":[]}""";

        var sut = CreateSutWithOpenAi(handler);

        var result = await sut.AnalyzeInventoryPhotoAsync(
            photoFileId: "photo-id",
            mode: TelegramInventoryPhotoMode.Restock,
            inventoryItems: [new TelegramInventoryItemHint(1, "Milk")],
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Candidates);
        Assert.Equal(189.50m, result.Candidates[0].PriceTotal);
        Assert.Equal(94.75m, result.Candidates[0].PricePerUnit);
    }

    [Fact]
    public async Task AnalyzeInventoryPhotoAsync_ShouldParsePricesAndNameEn_OnUnknownItems()
    {
        using var handler = new StubTelegramFileHttpMessageHandler();
        handler.AddFile("photo-id", "photos/receipt.jpg", [1, 2, 3, 4]);
        handler.OpenAiResponseJson = """{"candidates":[],"unknown":[{"name":"Сосиски","nameEn":"Sausages","quantity":1,"unit":"kg","confidence":0.62,"priceTotal":74.80,"pricePerUnit":200.00}]}""";

        var sut = CreateSutWithOpenAi(handler);

        var result = await sut.AnalyzeInventoryPhotoAsync(
            photoFileId: "photo-id",
            mode: TelegramInventoryPhotoMode.Restock,
            inventoryItems: [new TelegramInventoryItemHint(1, "Milk")],
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Unknown);
        Assert.Equal("Сосиски", result.Unknown[0].Name);
        Assert.Equal("Sausages", result.Unknown[0].NameEn);
        Assert.Equal(74.80m, result.Unknown[0].PriceTotal);
        Assert.Equal(200.00m, result.Unknown[0].PricePerUnit);
    }

    [Fact]
    public async Task AnalyzeInventoryPhotoAsync_ShouldSkipUnknown_WhenMarkedAsNonProduct()
    {
        using var handler = new StubTelegramFileHttpMessageHandler();
        handler.AddFile("photo-id", "photos/receipt.jpg", [1, 2, 3, 4]);
        handler.OpenAiResponseJson =
            """{"candidates":[],"unknown":[{"name":"Bag","nameEn":"Bag","quantity":1,"unit":"pcs","confidence":0.9,"isNonProduct":true},{"name":"Сосиски","nameEn":"Sausages","quantity":1,"unit":"kg","confidence":0.8,"isNonProduct":false}],"nonProducts":["Bag"]}""";

        var sut = CreateSutWithOpenAi(handler);

        var result = await sut.AnalyzeInventoryPhotoAsync(
            photoFileId: "photo-id",
            mode: TelegramInventoryPhotoMode.Restock,
            inventoryItems: [new TelegramInventoryItemHint(1, "Milk")],
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Unknown);
        Assert.Equal("Sausages", result.Unknown[0].NameEn);
    }

    [Fact]
    public async Task AnalyzeInventoryPhotoAsync_ShouldParseNonProductsArray()
    {
        using var handler = new StubTelegramFileHttpMessageHandler();
        handler.AddFile("photo-id", "photos/receipt.jpg", [1, 2, 3, 4]);
        handler.OpenAiResponseJson =
            """{"store":{"name":"АТБ","nameEn":"ATB","confidence":0.9},"candidates":[],"unknown":[],"nonProducts":["Bag","Delivery"]}""";

        var sut = CreateSutWithOpenAi(handler);

        var result = await sut.AnalyzeInventoryPhotoAsync(
            photoFileId: "photo-id",
            mode: TelegramInventoryPhotoMode.Restock,
            inventoryItems: [new TelegramInventoryItemHint(1, "Milk")],
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(["Bag", "Delivery"], result.NonProducts);
    }

    [Fact]
    public async Task AnalyzeInventoryPhotoAsync_ShouldReturnTimeoutError_WhenOpenAiTimesOut()
    {
        using var handler = new StubTelegramFileHttpMessageHandler();
        handler.AddFile("photo-id", "photos/receipt.jpg", [1, 2, 3, 4]);
        handler.TimeoutOnAllOpenAiCalls = true;

        var sut = CreateSutWithOpenAi(handler);

        var result = await sut.AnalyzeInventoryPhotoAsync(
            photoFileId: "photo-id",
            mode: TelegramInventoryPhotoMode.Consumption,
            inventoryItems: [new TelegramInventoryItemHint(1, "Milk")],
            cancellationToken: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("inventory.photo.timeout", result.Error);
    }

    [Fact]
    public async Task AnalyzeInventoryPhotoAsync_ShouldContinue_WhenOcrTimesOutInRestockMode()
    {
        using var handler = new StubTelegramFileHttpMessageHandler();
        handler.AddFile("photo-id", "photos/receipt.jpg", [1, 2, 3, 4]);
        handler.TimeoutOnOpenAiCallNumber = 1; // OCR call
        handler.OpenAiResponseJson = """{"candidates":[{"itemId":1,"quantity":1,"unit":"pcs","confidence":0.9}],"unknown":[]}""";

        var sut = CreateSutWithOpenAi(handler);

        var result = await sut.AnalyzeInventoryPhotoAsync(
            photoFileId: "photo-id",
            mode: TelegramInventoryPhotoMode.Restock,
            inventoryItems: [new TelegramInventoryItemHint(1, "Milk")],
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Candidates);
        Assert.Equal("Milk", result.Candidates[0].Name);
    }

    private static ITelegramImportSourceReader CreateSut(HttpMessageHandler handler)
    {
        return CreateSutInternal(handler, apiKey: null);
    }

    private static ITelegramImportSourceReader CreateSutWithOpenAi(HttpMessageHandler handler)
    {
        return CreateSutInternal(handler, apiKey: "test-key");
    }

    private static ITelegramImportSourceReader CreateSutInternal(HttpMessageHandler handler, string? apiKey)
    {
        var readerType = typeof(TelegramController).Assembly.GetType(
            "LagerthaAssistant.Api.Services.TelegramImportSourceReader",
            throwOnError: true)!;

        var loggerType = typeof(Logger<>).MakeGenericType(readerType);
        var loggerInstance = Activator.CreateInstance(loggerType, NullLoggerFactory.Instance)!;

        return (ITelegramImportSourceReader)Activator.CreateInstance(
            readerType,
            new StubHttpClientFactory(handler),
            Options.Create(new TelegramOptions
            {
                Enabled = true,
                BotToken = "bot-token",
                ApiBaseUrl = "https://api.telegram.org"
            }),
            new OpenAiOptions
            {
                ApiKey = apiKey
            },
            loggerInstance)!;
    }

    private static byte[] CreateSimpleWorkbookBytes(params string[] values)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(
                archive,
                "[Content_Types].xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                  <Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/>
                </Types>
                """);

            AddEntry(
                archive,
                "_rels/.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);

            AddEntry(
                archive,
                "xl/workbook.xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets>
                    <sheet name="Sheet1" sheetId="1" r:id="rId1"/>
                  </sheets>
                </workbook>
                """);

            AddEntry(
                archive,
                "xl/_rels/workbook.xml.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/>
                </Relationships>
                """);

            var sharedStrings = new StringBuilder();
            sharedStrings.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
            sharedStrings.AppendLine($"""<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="{values.Length}" uniqueCount="{values.Length}">""");
            foreach (var value in values)
            {
                sharedStrings.AppendLine("  <si><t>" + System.Security.SecurityElement.Escape(value) + "</t></si>");
            }
            sharedStrings.AppendLine("</sst>");
            AddEntry(archive, "xl/sharedStrings.xml", sharedStrings.ToString());

            var sheetData = new StringBuilder();
            sheetData.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
            sheetData.AppendLine("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
            for (var i = 0; i < values.Length; i++)
            {
                var row = i + 1;
                sheetData.AppendLine($"""  <row r="{row}"><c r="A{row}" t="s"><v>{i}</v></c></row>""");
            }

            sheetData.AppendLine("</sheetData></worksheet>");
            AddEntry(archive, "xl/worksheets/sheet1.xml", sheetData.ToString());
        }

        return stream.ToArray();
    }

    private static byte[] CreateSimpleDocxBytes(params string[] paragraphs)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(
                archive,
                "[Content_Types].xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
                </Types>
                """);

            AddEntry(
                archive,
                "_rels/.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
                </Relationships>
                """);

            var documentXml = new StringBuilder();
            documentXml.AppendLine("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
            documentXml.AppendLine("""<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>""");
            foreach (var paragraph in paragraphs)
            {
                documentXml.AppendLine($"<w:p><w:r><w:t>{System.Security.SecurityElement.Escape(paragraph)}</w:t></w:r></w:p>");
            }

            documentXml.AppendLine("</w:body></w:document>");
            AddEntry(archive, "word/document.xml", documentXml.ToString());
        }

        return stream.ToArray();
    }

    private static void AddEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class StubTelegramFileHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (string Path, byte[] Content)> _files = new(StringComparer.Ordinal);
        private int _openAiCalls;

        /// <summary>
        /// When set, any POST to an OpenAI chat/completions endpoint will return this JSON
        /// wrapped in a standard chat completion response envelope.
        /// </summary>
        public string? OpenAiResponseJson { get; set; }
        public bool TimeoutOnAllOpenAiCalls { get; set; }
        public int? TimeoutOnOpenAiCallNumber { get; set; }

        public void AddFile(string fileId, string filePath, byte[] bytes)
        {
            _files[fileId] = (filePath, bytes);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? throw new InvalidOperationException("Request URI is missing.");
            var pathAndQuery = uri.PathAndQuery;

            // Intercept OpenAI chat/completions calls
            if (OpenAiResponseJson is not null
                && request.Method == HttpMethod.Post
                && pathAndQuery.Contains("chat/completions", StringComparison.Ordinal))
            {
                _openAiCalls++;
                if (TimeoutOnAllOpenAiCalls
                    || (TimeoutOnOpenAiCallNumber.HasValue && TimeoutOnOpenAiCallNumber.Value == _openAiCalls))
                {
                    throw new TaskCanceledException("Simulated timeout.");
                }

                var envelope = JsonSerializer.Serialize(new
                {
                    choices = new[]
                    {
                        new { message = new { content = OpenAiResponseJson } }
                    }
                });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(envelope, Encoding.UTF8, "application/json")
                });
            }

            if ((TimeoutOnAllOpenAiCalls || TimeoutOnOpenAiCallNumber.HasValue)
                && request.Method == HttpMethod.Post
                && pathAndQuery.Contains("chat/completions", StringComparison.Ordinal))
            {
                _openAiCalls++;
                if (TimeoutOnAllOpenAiCalls
                    || (TimeoutOnOpenAiCallNumber.HasValue && TimeoutOnOpenAiCallNumber.Value == _openAiCalls))
                {
                    throw new TaskCanceledException("Simulated timeout.");
                }
            }

            if (pathAndQuery.Contains("/getFile?", StringComparison.Ordinal))
            {
                var fileId = GetQueryValue(uri.Query, "file_id");
                if (!_files.TryGetValue(fileId, out var file))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                    {
                        Content = new StringContent("{\"ok\":false}", Encoding.UTF8, "application/json")
                    });
                }

                var payload = JsonSerializer.Serialize(new
                {
                    ok = true,
                    result = new
                    {
                        file_path = file.Path
                    }
                });

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                });
            }

            foreach (var file in _files.Values)
            {
                if (pathAndQuery.EndsWith("/" + file.Path, StringComparison.Ordinal))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(file.Content)
                    });
                }
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
        }

        private static string GetQueryValue(string queryString, string key)
        {
            if (string.IsNullOrWhiteSpace(queryString))
            {
                return string.Empty;
            }

            var query = queryString.StartsWith("?", StringComparison.Ordinal)
                ? queryString[1..]
                : queryString;

            foreach (var segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = segment.Split('=', 2);
                if (pair.Length == 0 || !string.Equals(pair[0], key, StringComparison.Ordinal))
                {
                    continue;
                }

                return pair.Length == 2 ? Uri.UnescapeDataString(pair[1]) : string.Empty;
            }

            return string.Empty;
        }
    }
}
