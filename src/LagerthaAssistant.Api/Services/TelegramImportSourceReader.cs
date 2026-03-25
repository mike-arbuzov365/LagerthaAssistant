using System.Net.Http.Headers;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ExcelDataReader;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Api.Options;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Options;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;

namespace LagerthaAssistant.Api.Services;

internal sealed class TelegramImportSourceReader : ITelegramImportSourceReader
{
    private const int MaxImportBytes = 4_000_000;
    private const int MaxPhotoBytes = 4_000_000;
    private const string MimeTypePdf = "application/pdf";
    private static int _codePageProviderRegistered;

    private static readonly HashSet<string> SupportedFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".md",
        ".csv",
        ".log",
        ".json",
        ".pdf",
        ".docx",
        ".xlsx",
        ".xls",
        ".xlsm"
    };

    private static readonly HashSet<string> ExcelExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx",
        ".xls",
        ".xlsm"
    };

    private static readonly HashSet<string> ExcelMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-excel.sheet.macroEnabled.12"
    };

    private static readonly HashSet<string> WordExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".docx"
    };

    private static readonly HashSet<string> WordMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TelegramOptions _telegramOptions;
    private readonly OpenAiOptions _openAiOptions;
    private readonly ILogger<TelegramImportSourceReader> _logger;

    public TelegramImportSourceReader(
        IHttpClientFactory httpClientFactory,
        IOptions<TelegramOptions> telegramOptions,
        OpenAiOptions openAiOptions,
        ILogger<TelegramImportSourceReader> logger)
    {
        _httpClientFactory = httpClientFactory;
        _telegramOptions = telegramOptions.Value;
        _openAiOptions = openAiOptions;
        _logger = logger;
    }

    public async Task<TelegramImportSourceReadResult> ReadTextAsync(
        TelegramImportInbound inbound,
        TelegramImportSourceType sourceType,
        CancellationToken cancellationToken = default)
    {
        return sourceType switch
        {
            TelegramImportSourceType.Url => ReadUrl(inbound),
            TelegramImportSourceType.Text => ReadText(inbound),
            TelegramImportSourceType.File => await ReadDocumentAsync(inbound, cancellationToken),
            TelegramImportSourceType.Photo => await ReadPhotoAsync(inbound, cancellationToken),
            _ => new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.Failed, Error: "Unsupported source type.")
        };
    }

    public async Task<TelegramFoodIdentificationResult> IdentifyFoodAsync(
        string photoFileId,
        CancellationToken cancellationToken = default)
    {
        var download = await DownloadTelegramFileAsync(photoFileId, MaxPhotoBytes, cancellationToken);
        if (!download.Success)
            return new TelegramFoodIdentificationResult(false, Error: download.Error);

        return await IdentifyFoodFromImageAsync(download.Content!, cancellationToken);
    }

    public async Task<TelegramInventoryPhotoAnalysisResult> AnalyzeInventoryPhotoAsync(
        string photoFileId,
        TelegramInventoryPhotoMode mode,
        IReadOnlyList<TelegramInventoryItemHint> inventoryItems,
        CancellationToken cancellationToken = default)
    {
        if (inventoryItems.Count == 0)
        {
            return new TelegramInventoryPhotoAnalysisResult(
                Success: false,
                Candidates: [],
                Unknown: [],
                Error: "Inventory is empty.");
        }

        var download = await DownloadTelegramFileAsync(photoFileId, MaxPhotoBytes, cancellationToken);
        if (!download.Success)
        {
            return new TelegramInventoryPhotoAnalysisResult(false, [], [], Error: download.Error);
        }

        if (string.IsNullOrWhiteSpace(_openAiOptions.ApiKey))
        {
            return new TelegramInventoryPhotoAnalysisResult(false, [], [], Error: "OpenAI API key is not configured.");
        }

        var inventoryLookup = inventoryItems.ToDictionary(x => x.Id, x => x.Name);
        var inventoryLines = inventoryItems
            .OrderBy(x => x.Id)
            .Select(x => $"{x.Id}|{x.Name}")
            .ToArray();

        var baseUrl = string.IsNullOrWhiteSpace(_openAiOptions.BaseUrl)
            ? OpenAiConstants.DefaultBaseUrl
            : _openAiOptions.BaseUrl.Trim();
        if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
        {
            baseUrl += "/";
        }

        var endpoint = new Uri(new Uri(baseUrl), OpenAiConstants.ChatCompletionsEndpoint);
        var dataUrl = $"data:image/jpeg;base64,{Convert.ToBase64String(download.Content!)}";
        var modeText = mode == TelegramInventoryPhotoMode.Consumption ? "consumption" : "restock";
        var ocrText = string.Empty;

        // For restock mode, OCR helps when users send a receipt photo instead of product photo.
        if (mode == TelegramInventoryPhotoMode.Restock)
        {
            var ocr = await ExtractTextFromImageAsync(download.Content!, "image/jpeg", cancellationToken);
            if (ocr.Success && !string.IsNullOrWhiteSpace(ocr.Text))
            {
                ocrText = ocr.Text.Trim();
            }
        }

        var prompt = new StringBuilder();
        prompt.AppendLine("Detect inventory quantity changes from this image.");
        prompt.AppendLine($"Mode: {modeText}.");
        prompt.AppendLine("Use only inventory IDs from the list below.");
        prompt.AppendLine("The image can be either products on a table OR a receipt photo.");
        prompt.AppendLine("Return strict JSON only:");
        prompt.AppendLine("{\"store\":{\"name\":\"Store Name\",\"nameEn\":\"Store Name In English\",\"confidence\":0.95},\"candidates\":[{\"itemId\":20,\"quantity\":2,\"unit\":\"pcs\",\"confidence\":0.91,\"priceTotal\":189.50,\"pricePerUnit\":251.66}],\"unknown\":[{\"name\":\"Сосиски\",\"nameEn\":\"Sausages\",\"quantity\":1,\"unit\":\"kg\",\"confidence\":0.62,\"priceTotal\":74.80,\"pricePerUnit\":200.00,\"isNonProduct\":false}]}");
        prompt.AppendLine("Rules:");
        prompt.AppendLine("- quantity must be > 0.");
        prompt.AppendLine("- if image is a receipt and mode=restock, infer quantities from line items; if qty is missing, default to 1.");
        prompt.AppendLine("- confidence range 0..1.");
        prompt.AppendLine("- unknown list only for products not present in the inventory list.");
        prompt.AppendLine("- no markdown, no commentary.");
        prompt.AppendLine("- Receipt items in Ukrainian; inventory names in English. Match Ukrainian receipt text to English inventory names.");
        prompt.AppendLine("- store: detect store/shop name from receipt header. name=original language, nameEn=English translation. Omit if not a receipt or store not detected.");
        prompt.AppendLine("- priceTotal: total price for the line item from receipt. pricePerUnit: price per kg/L/pcs. Omit both if not a receipt or prices not visible.");
        prompt.AppendLine("- For unknown items: name=original language from receipt, nameEn=English translation (concise product name, no brand). isNonProduct=true for bags, packaging, delivery fees, discounts, loyalty cards — anything that is NOT an actual food/household product.");
        prompt.AppendLine("- Do NOT include non-product items in the unknown list. Only include actual food or household products that could be added to inventory.");
        if (!string.IsNullOrWhiteSpace(ocrText))
        {
            prompt.AppendLine("OCR text extracted from image (use as extra signal):");
            prompt.AppendLine(ocrText.Length <= 6000 ? ocrText : ocrText[..6000]);
        }
        prompt.AppendLine("Inventory list (id|name):");
        prompt.AppendLine(string.Join('\n', inventoryLines));

        var requestBody = new
        {
            model = _openAiOptions.Model,
            temperature = 0.0,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You are an inventory photo parser. Return strict JSON only."
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = prompt.ToString()
                        },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = dataUrl
                            }
                        }
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiOptions.ApiKey);

        using var client = _httpClientFactory.CreateClient("vocab-discovery");
        using var response = await client.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Inventory photo analysis failed with status {StatusCode}: {Body}", (int)response.StatusCode, raw);
            return new TelegramInventoryPhotoAnalysisResult(
                false,
                [],
                [],
                Error: $"Inventory photo analysis request failed ({(int)response.StatusCode}).");
        }

        try
        {
            using var completionDoc = JsonDocument.Parse(raw);
            var assistantText = ExtractAssistantText(completionDoc.RootElement);
            if (string.IsNullOrWhiteSpace(assistantText))
            {
                return new TelegramInventoryPhotoAnalysisResult(false, [], [], Error: "Empty response from Vision API.");
            }

            var normalizedJson = NormalizeJsonPayload(assistantText);
            using var jsonDoc = JsonDocument.Parse(normalizedJson);
            var root = jsonDoc.RootElement;

            var candidates = ParseInventoryCandidates(root, inventoryLookup);
            var unknown = ParseInventoryUnknown(root);
            var detectedStore = ParseDetectedStore(root);
            return new TelegramInventoryPhotoAnalysisResult(true, candidates, unknown, detectedStore);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse inventory photo analysis response.");
            return new TelegramInventoryPhotoAnalysisResult(false, [], [], Error: "Failed to parse inventory photo response.");
        }
    }

    private async Task<TelegramFoodIdentificationResult> IdentifyFoodFromImageAsync(
        byte[] imageBytes,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_openAiOptions.ApiKey))
            return new TelegramFoodIdentificationResult(false, Error: "OpenAI API key is not configured.");

        var baseUrl = string.IsNullOrWhiteSpace(_openAiOptions.BaseUrl)
            ? OpenAiConstants.DefaultBaseUrl
            : _openAiOptions.BaseUrl.Trim();
        if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
            baseUrl += "/";

        var endpoint = new Uri(new Uri(baseUrl), OpenAiConstants.ChatCompletionsEndpoint);
        var dataUrl = $"data:image/jpeg;base64,{Convert.ToBase64String(imageBytes)}";

        var requestBody = new
        {
            model = _openAiOptions.Model,
            temperature = 0.0,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You identify food from photos. Respond with exactly two lines:\nLine 1: meal name\nLine 2: estimated calories per serving (number only)\nIf you cannot identify food, respond with \"unknown\" on line 1 and \"0\" on line 2."
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = "What food is in this photo? Give me the meal name and estimated calories."
                        },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = dataUrl
                            }
                        }
                    }
                }
            }
        };

        var payload = JsonSerializer.Serialize(requestBody);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiOptions.ApiKey);

        using var client = _httpClientFactory.CreateClient("vocab-discovery");
        using var response = await client.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Food identification request failed with status {StatusCode}: {Body}", (int)response.StatusCode, raw);
            return new TelegramFoodIdentificationResult(false, Error: $"Food identification request failed ({(int)response.StatusCode}).");
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var text = ExtractAssistantText(doc.RootElement);
            return ParseFoodIdentificationResponse(text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse food identification response.");
            return new TelegramFoodIdentificationResult(false, Error: "Failed to parse food identification response.");
        }
    }

    private static TelegramFoodIdentificationResult ParseFoodIdentificationResponse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new TelegramFoodIdentificationResult(false, Error: "Empty response from Vision API.");

        var lines = text.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2)
            return new TelegramFoodIdentificationResult(false, Error: "Unexpected response format from Vision API.");

        var mealName = lines[0].Trim();
        var caloriesText = lines[1].Trim();

        // Extract digits from calories line
        var digitsOnly = new string(caloriesText.Where(char.IsDigit).ToArray());
        if (!int.TryParse(digitsOnly, out var calories) || calories <= 0)
            calories = 0;

        if (string.Equals(mealName, "unknown", StringComparison.OrdinalIgnoreCase) || calories == 0)
            return new TelegramFoodIdentificationResult(false, Error: "Could not identify food in the photo.");

        return new TelegramFoodIdentificationResult(true, mealName, calories);
    }

    private static TelegramImportSourceReadResult ReadUrl(TelegramImportInbound inbound)
    {
        if (!string.IsNullOrWhiteSpace(inbound.PhotoFileId) || !string.IsNullOrWhiteSpace(inbound.DocumentFileId))
        {
            return new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.WrongInputType);
        }

        var text = inbound.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.InvalidSource);
        }

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.Success, text);
        }

        return new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.InvalidSource);
    }

    private static TelegramImportSourceReadResult ReadText(TelegramImportInbound inbound)
    {
        if (!string.IsNullOrWhiteSpace(inbound.PhotoFileId) || !string.IsNullOrWhiteSpace(inbound.DocumentFileId))
        {
            return new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.WrongInputType);
        }

        var text = inbound.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.InvalidSource);
        }

        return new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.Success, text);
    }

    private async Task<TelegramImportSourceReadResult> ReadDocumentAsync(
        TelegramImportInbound inbound,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inbound.DocumentFileId))
        {
            return new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.WrongInputType);
        }

        if (!IsSupportedDocumentType(inbound.DocumentFileName, inbound.DocumentMimeType))
        {
            return new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.UnsupportedFileType);
        }

        var download = await DownloadTelegramFileAsync(inbound.DocumentFileId, MaxImportBytes, cancellationToken);
        if (!download.Success)
        {
            return new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.Failed, Error: download.Error);
        }

        var parse = ParseDocumentContent(download.Content!, inbound.DocumentFileName, inbound.DocumentMimeType);
        if (!parse.Success)
        {
            return new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.Failed, Error: parse.Error);
        }

        var content = parse.Content ?? string.Empty;
        if (string.IsNullOrWhiteSpace(content))
        {
            return new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.NoTextExtracted);
        }

        return new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.Success, content.Trim());
    }

    private async Task<TelegramImportSourceReadResult> ReadPhotoAsync(
        TelegramImportInbound inbound,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inbound.PhotoFileId))
        {
            return new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.WrongInputType);
        }

        var download = await DownloadTelegramFileAsync(inbound.PhotoFileId, MaxPhotoBytes, cancellationToken);
        if (!download.Success)
        {
            return new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.Failed, Error: download.Error);
        }

        var ocr = await ExtractTextFromImageAsync(download.Content!, "image/jpeg", cancellationToken);
        if (!ocr.Success)
        {
            return new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.Failed, Error: ocr.Error);
        }

        if (string.IsNullOrWhiteSpace(ocr.Text))
        {
            return new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.NoTextExtracted);
        }

        return new TelegramImportSourceReadResult(TelegramImportSourceReadStatus.Success, ocr.Text.Trim());
    }

    private static bool IsSupportedDocumentType(string? fileName, string? mimeType)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(extension) && SupportedFileExtensions.Contains(extension))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return false;
        }

        return mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
               || string.Equals(mimeType, "application/json", StringComparison.OrdinalIgnoreCase)
               || string.Equals(mimeType, "text/csv", StringComparison.OrdinalIgnoreCase)
               || string.Equals(mimeType, MimeTypePdf, StringComparison.OrdinalIgnoreCase)
               || ExcelMimeTypes.Contains(mimeType)
               || WordMimeTypes.Contains(mimeType);
    }

    private ParsedDocumentContent ParseDocumentContent(byte[] bytes, string? fileName, string? mimeType)
    {
        var documentKind = ResolveDocumentKind(fileName, mimeType);

        try
        {
            var content = documentKind switch
            {
                DocumentKind.Pdf => ExtractTextFromPdf(bytes),
                DocumentKind.Excel => ExtractTextFromExcel(bytes),
                DocumentKind.Word => ExtractTextFromWord(bytes),
                _ => DecodeText(bytes)
            };

            return ParsedDocumentContent.Ok(content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to parse imported document. FileName={FileName}; MimeType={MimeType}; Kind={Kind}",
                fileName ?? string.Empty,
                mimeType ?? string.Empty,
                documentKind);

            return ParsedDocumentContent.Fail("Document parsing failed.");
        }
    }

    private static DocumentKind ResolveDocumentKind(string? fileName, string? mimeType)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mimeType, MimeTypePdf, StringComparison.OrdinalIgnoreCase))
        {
            return DocumentKind.Pdf;
        }

        if (ExcelExtensions.Contains(extension) || (!string.IsNullOrWhiteSpace(mimeType) && ExcelMimeTypes.Contains(mimeType)))
        {
            return DocumentKind.Excel;
        }

        if (WordExtensions.Contains(extension) || (!string.IsNullOrWhiteSpace(mimeType) && WordMimeTypes.Contains(mimeType)))
        {
            return DocumentKind.Word;
        }

        return DocumentKind.Text;
    }

    private static string ExtractTextFromPdf(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var document = PdfDocument.Open(stream);

        var builder = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            if (string.IsNullOrWhiteSpace(page.Text))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(page.Text.Trim());
        }

        return builder.ToString();
    }

    private static string ExtractTextFromExcel(byte[] bytes)
    {
        EnsureCodePageProviderRegistered();

        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        var values = new List<string>();
        do
        {
            while (reader.Read())
            {
                for (var column = 0; column < reader.FieldCount; column++)
                {
                    var cellValue = reader.GetValue(column);
                    if (cellValue is null)
                    {
                        continue;
                    }

                    var text = Convert.ToString(cellValue);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    values.Add(text.Trim());
                }
            }
        } while (reader.NextResult());

        return string.Join(Environment.NewLine, values);
    }

    private static string ExtractTextFromWord(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var documentEntry = archive.GetEntry("word/document.xml");
        if (documentEntry is null)
        {
            return string.Empty;
        }

        using var docStream = documentEntry.Open();
        var document = XDocument.Load(docStream, LoadOptions.None);
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        var paragraphs = document
            .Descendants(w + "p")
            .Select(paragraph => string.Concat(
                paragraph
                    .Descendants(w + "t")
                    .Select(textNode => textNode.Value)))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text.Trim())
            .ToList();

        return string.Join(Environment.NewLine, paragraphs);
    }

    private async Task<TelegramDownloadResult> DownloadTelegramFileAsync(
        string fileId,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        if (!_telegramOptions.Enabled)
        {
            return TelegramDownloadResult.Fail("Telegram integration is disabled.");
        }

        if (string.IsNullOrWhiteSpace(_telegramOptions.BotToken))
        {
            return TelegramDownloadResult.Fail("Telegram bot token is missing.");
        }

        var filePathResult = await ResolveFilePathAsync(fileId, cancellationToken);
        if (!filePathResult.Success || string.IsNullOrWhiteSpace(filePathResult.FilePath))
        {
            return TelegramDownloadResult.Fail(filePathResult.Error ?? "Telegram file path not found.");
        }

        var baseUrl = NormalizeApiBaseUrl(_telegramOptions.ApiBaseUrl);
        var fileUri = new Uri($"{baseUrl}/file/bot{_telegramOptions.BotToken}/{filePathResult.FilePath}");

        using var request = new HttpRequestMessage(HttpMethod.Get, fileUri);
        using var client = _httpClientFactory.CreateClient("telegram");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            return TelegramDownloadResult.Fail($"Telegram file download failed ({(int)response.StatusCode}): {error}");
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length == 0)
        {
            return TelegramDownloadResult.Fail("Telegram file is empty.");
        }

        if (bytes.Length > maxBytes)
        {
            return TelegramDownloadResult.Fail($"Telegram file is too large ({bytes.Length} bytes).");
        }

        return TelegramDownloadResult.Ok(bytes);
    }

    private async Task<TelegramFilePathResult> ResolveFilePathAsync(string fileId, CancellationToken cancellationToken)
    {
        var baseUrl = NormalizeApiBaseUrl(_telegramOptions.ApiBaseUrl);
        var uri = new Uri($"{baseUrl}/bot{_telegramOptions.BotToken}/getFile?file_id={Uri.EscapeDataString(fileId)}");

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var client = _httpClientFactory.CreateClient("telegram");
        using var response = await client.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return TelegramFilePathResult.Fail($"Telegram getFile failed ({(int)response.StatusCode}): {raw}");
        }

        try
        {
            using var json = JsonDocument.Parse(raw);
            var root = json.RootElement;
            var ok = root.TryGetProperty("ok", out var okValue) && okValue.ValueKind == JsonValueKind.True;
            if (!ok)
            {
                return TelegramFilePathResult.Fail("Telegram getFile returned ok=false.");
            }

            if (!root.TryGetProperty("result", out var result))
            {
                return TelegramFilePathResult.Fail("Telegram getFile response has no result.");
            }

            if (!result.TryGetProperty("file_path", out var pathElement)
                || pathElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(pathElement.GetString()))
            {
                return TelegramFilePathResult.Fail("Telegram getFile response has no file_path.");
            }

            return TelegramFilePathResult.Ok(pathElement.GetString()!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse Telegram getFile response.");
            return TelegramFilePathResult.Fail("Failed to parse Telegram getFile response.");
        }
    }

    private async Task<OcrResult> ExtractTextFromImageAsync(
        byte[] imageBytes,
        string mimeType,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_openAiOptions.ApiKey))
        {
            return OcrResult.Fail("OpenAI API key is not configured.");
        }

        var baseUrl = string.IsNullOrWhiteSpace(_openAiOptions.BaseUrl)
            ? OpenAiConstants.DefaultBaseUrl
            : _openAiOptions.BaseUrl.Trim();
        if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
        {
            baseUrl += "/";
        }

        var endpoint = new Uri(new Uri(baseUrl), OpenAiConstants.ChatCompletionsEndpoint);
        var dataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(imageBytes)}";

        var requestBody = new
        {
            model = _openAiOptions.Model,
            temperature = 0.0,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You extract text from images. Return only extracted plain text. No explanations."
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = "Extract all readable text from this image."
                        },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = dataUrl
                            }
                        }
                    }
                }
            }
        };

        var payload = JsonSerializer.Serialize(requestBody);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiOptions.ApiKey);

        using var client = _httpClientFactory.CreateClient("vocab-discovery");
        using var response = await client.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Image OCR request failed with status {StatusCode}: {Body}", (int)response.StatusCode, raw);
            return OcrResult.Fail($"OCR request failed ({(int)response.StatusCode}).");
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var text = ExtractAssistantText(doc.RootElement);
            return OcrResult.Ok(text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse OCR response.");
            return OcrResult.Fail("Failed to parse OCR response.");
        }
    }

    private static string ExtractAssistantText(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var firstChoice = choices[0];
        if (!firstChoice.TryGetProperty("message", out var message)
            || !message.TryGetProperty("content", out var content))
        {
            return string.Empty;
        }

        return content.ValueKind switch
        {
            JsonValueKind.String => content.GetString() ?? string.Empty,
            JsonValueKind.Array => string.Join(string.Empty, content.EnumerateArray()
                .Where(x => x.TryGetProperty("type", out var type) && type.GetString() == "text")
                .Select(x => x.TryGetProperty("text", out var text) ? text.GetString() : string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))),
            _ => string.Empty
        };
    }

    private static string NormalizeJsonPayload(string assistantText)
    {
        var trimmed = assistantText.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var start = trimmed.IndexOf('{');
            var end = trimmed.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                return trimmed[start..(end + 1)];
            }
        }

        return trimmed;
    }

    private static IReadOnlyList<TelegramInventoryPhotoCandidate> ParseInventoryCandidates(
        JsonElement root,
        IReadOnlyDictionary<int, string> inventoryLookup)
    {
        var result = new List<TelegramInventoryPhotoCandidate>();
        if (!root.TryGetProperty("candidates", out var candidatesElement)
            || candidatesElement.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var element in candidatesElement.EnumerateArray())
        {
            if (!TryGetInt(element, "itemId", out var itemId))
            {
                continue;
            }

            if (!inventoryLookup.TryGetValue(itemId, out var name))
            {
                continue;
            }

            if (!TryGetDecimal(element, "quantity", out var quantity) || quantity <= 0m)
            {
                continue;
            }

            var unit = TryGetString(element, "unit");
            var confidence = TryGetDouble(element, "confidence", 0.75);
            var priceTotal = TryGetNullableDecimal(element, "priceTotal");
            var pricePerUnit = TryGetNullableDecimal(element, "pricePerUnit");

            result.Add(new TelegramInventoryPhotoCandidate(
                itemId,
                name,
                Math.Round(quantity, 3, MidpointRounding.AwayFromZero),
                unit,
                Math.Clamp(confidence, 0d, 1d),
                priceTotal,
                pricePerUnit));
        }

        return result;
    }

    private static IReadOnlyList<TelegramInventoryPhotoUnknown> ParseInventoryUnknown(JsonElement root)
    {
        var result = new List<TelegramInventoryPhotoUnknown>();
        if (!root.TryGetProperty("unknown", out var unknownElement)
            || unknownElement.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var element in unknownElement.EnumerateArray())
        {
            var name = TryGetString(element, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!TryGetDecimal(element, "quantity", out var quantity) || quantity <= 0m)
            {
                continue;
            }

            var unit = TryGetString(element, "unit");
            var confidence = TryGetDouble(element, "confidence", 0.5);
            var nameEn = TryGetString(element, "nameEn");
            var priceTotal = TryGetNullableDecimal(element, "priceTotal");
            var pricePerUnit = TryGetNullableDecimal(element, "pricePerUnit");

            result.Add(new TelegramInventoryPhotoUnknown(
                name.Trim(),
                nameEn?.Trim(),
                Math.Round(quantity, 3, MidpointRounding.AwayFromZero),
                unit,
                Math.Clamp(confidence, 0d, 1d),
                priceTotal,
                pricePerUnit));
        }

        return result;
    }

    private static TelegramInventoryPhotoDetectedStore? ParseDetectedStore(JsonElement root)
    {
        if (!root.TryGetProperty("store", out var storeElement)
            || storeElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var name = TryGetString(storeElement, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var nameEn = TryGetString(storeElement, "nameEn");
        var confidence = TryGetDouble(storeElement, "confidence", 0.5);
        return new TelegramInventoryPhotoDetectedStore(name.Trim(), nameEn?.Trim(), Math.Clamp(confidence, 0d, 1d));
    }

    private static decimal? TryGetNullableDecimal(JsonElement element, string propertyName)
    {
        return TryGetDecimal(element, propertyName, out var value) && value > 0m
            ? Math.Round(value, 2, MidpointRounding.AwayFromZero)
            : null;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null
        };
    }

    private static bool TryGetInt(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value))
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.String
            && int.TryParse(property.GetString(), out value))
        {
            return true;
        }

        return false;
    }

    private static bool TryGetDecimal(JsonElement element, string propertyName, out decimal value)
    {
        value = 0m;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out value))
        {
            return true;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            var raw = property.GetString()?.Replace(',', '.');
            return decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        return false;
    }

    private static double TryGetDouble(JsonElement element, string propertyName, double fallback)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return fallback;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var numeric))
        {
            return numeric;
        }

        if (property.ValueKind == JsonValueKind.String
            && double.TryParse(
                property.GetString()?.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static string DecodeText(byte[] bytes)
    {
        if (bytes.Length >= 3
            && bytes[0] == 0xEF
            && bytes[1] == 0xBB
            && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static string NormalizeApiBaseUrl(string? value)
    {
        var baseUrl = string.IsNullOrWhiteSpace(value)
            ? "https://api.telegram.org"
            : value.Trim();

        return baseUrl.TrimEnd('/');
    }

    private static void EnsureCodePageProviderRegistered()
    {
        if (Interlocked.Exchange(ref _codePageProviderRegistered, 1) == 0)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
    }

    private enum DocumentKind
    {
        Text = 0,
        Pdf = 1,
        Excel = 2,
        Word = 3
    }

    private sealed record TelegramDownloadResult(bool Success, byte[]? Content = null, string? Error = null)
    {
        public static TelegramDownloadResult Ok(byte[] content) => new(true, content);

        public static TelegramDownloadResult Fail(string error) => new(false, null, error);
    }

    private sealed record TelegramFilePathResult(bool Success, string? FilePath = null, string? Error = null)
    {
        public static TelegramFilePathResult Ok(string filePath) => new(true, filePath);

        public static TelegramFilePathResult Fail(string error) => new(false, null, error);
    }

    private sealed record OcrResult(bool Success, string? Text = null, string? Error = null)
    {
        public static OcrResult Ok(string? text) => new(true, text);

        public static OcrResult Fail(string error) => new(false, null, error);
    }

    private sealed record ParsedDocumentContent(bool Success, string? Content = null, string? Error = null)
    {
        public static ParsedDocumentContent Ok(string? content) => new(true, content);

        public static ParsedDocumentContent Fail(string error) => new(false, null, error);
    }
}
