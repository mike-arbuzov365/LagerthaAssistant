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

    private static ITelegramImportSourceReader CreateSut(HttpMessageHandler handler)
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
                ApiKey = null
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

        public void AddFile(string fileId, string filePath, byte[] bytes)
        {
            _files[fileId] = (filePath, bytes);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri ?? throw new InvalidOperationException("Request URI is missing.");
            var pathAndQuery = uri.PathAndQuery;

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
