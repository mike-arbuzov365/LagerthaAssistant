namespace LagerthaAssistant.IntegrationTests.Services;

using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Api.Services;
using Xunit;

public sealed class TelegramMediaIntentResolverTests
{
    // ── ClassifyMedia ────────────────────────────────────────────────────────

    [Fact]
    public void ClassifyMedia_WithPhotoFileId_ReturnsPhoto()
    {
        var kind = TelegramMediaIntentResolver.ClassifyMedia("AgACAgI_photo123", null, null);

        Assert.Equal(TelegramMediaKind.Photo, kind);
    }

    [Fact]
    public void ClassifyMedia_WithPhotoFileId_IgnoresDocument()
    {
        var kind = TelegramMediaIntentResolver.ClassifyMedia("photo123", "application/pdf", "file.pdf");

        Assert.Equal(TelegramMediaKind.Photo, kind);
    }

    [Theory]
    [InlineData("image/jpeg", null)]
    [InlineData("image/png", null)]
    [InlineData("image/webp", null)]
    [InlineData(null, "photo.jpg")]
    [InlineData(null, "photo.jpeg")]
    [InlineData(null, "photo.png")]
    [InlineData(null, "photo.webp")]
    [InlineData(null, "photo.bmp")]
    [InlineData(null, "photo.heic")]
    public void ClassifyMedia_WithImageDocument_ReturnsImageDocument(string? mime, string? fileName)
    {
        var kind = TelegramMediaIntentResolver.ClassifyMedia(null, mime, fileName);

        Assert.Equal(TelegramMediaKind.ImageDocument, kind);
    }

    [Theory]
    [InlineData("application/pdf", null)]
    [InlineData(null, "document.pdf")]
    public void ClassifyMedia_WithPdf_ReturnsPdfDocument(string? mime, string? fileName)
    {
        var kind = TelegramMediaIntentResolver.ClassifyMedia(null, mime, fileName);

        Assert.Equal(TelegramMediaKind.PdfDocument, kind);
    }

    [Theory]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", null)]
    [InlineData("application/vnd.ms-excel", null)]
    [InlineData("text/csv", null)]
    [InlineData(null, "data.xlsx")]
    [InlineData(null, "data.xls")]
    [InlineData(null, "data.csv")]
    public void ClassifyMedia_WithSpreadsheet_ReturnsSpreadsheet(string? mime, string? fileName)
    {
        var kind = TelegramMediaIntentResolver.ClassifyMedia(null, mime, fileName);

        Assert.Equal(TelegramMediaKind.Spreadsheet, kind);
    }

    [Theory]
    [InlineData("text/plain", null)]
    [InlineData("text/markdown", null)]
    [InlineData("application/msword", null)]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document", null)]
    [InlineData(null, "notes.txt")]
    [InlineData(null, "notes.md")]
    [InlineData(null, "notes.doc")]
    [InlineData(null, "notes.docx")]
    public void ClassifyMedia_WithTextDocument_ReturnsTextDocument(string? mime, string? fileName)
    {
        var kind = TelegramMediaIntentResolver.ClassifyMedia(null, mime, fileName);

        Assert.Equal(TelegramMediaKind.TextDocument, kind);
    }

    [Fact]
    public void ClassifyMedia_WithNullInputs_ReturnsUnknown()
    {
        var kind = TelegramMediaIntentResolver.ClassifyMedia(null, null, null);

        Assert.Equal(TelegramMediaKind.Unknown, kind);
    }

    [Fact]
    public void ClassifyMedia_WithUnrecognizedMime_ReturnsUnknown()
    {
        var kind = TelegramMediaIntentResolver.ClassifyMedia(null, "application/octet-stream", "binary.bin");

        Assert.Equal(TelegramMediaKind.Unknown, kind);
    }

    // ── ResolveCapabilities ──────────────────────────────────────────────────

    [Theory]
    [InlineData(TelegramMediaKind.Photo)]
    [InlineData(TelegramMediaKind.ImageDocument)]
    public void ResolveCapabilities_ForImageMedia_ReturnsAllFourCapabilities(TelegramMediaKind kind)
    {
        var caps = TelegramMediaIntentResolver.ResolveCapabilities(kind);

        Assert.Contains(TelegramMediaCapability.VocabImport, caps);
        Assert.Contains(TelegramMediaCapability.InventoryRestock, caps);
        Assert.Contains(TelegramMediaCapability.InventoryConsume, caps);
        Assert.Contains(TelegramMediaCapability.FoodPhoto, caps);
        Assert.Equal(4, caps.Count);
    }

    [Theory]
    [InlineData(TelegramMediaKind.TextDocument)]
    [InlineData(TelegramMediaKind.Spreadsheet)]
    [InlineData(TelegramMediaKind.PdfDocument)]
    public void ResolveCapabilities_ForDocumentMedia_ReturnsOnlyVocabImport(TelegramMediaKind kind)
    {
        var caps = TelegramMediaIntentResolver.ResolveCapabilities(kind);

        Assert.Equal([TelegramMediaCapability.VocabImport], caps);
    }

    [Fact]
    public void ResolveCapabilities_ForUnknownMedia_ReturnsEmpty()
    {
        var caps = TelegramMediaIntentResolver.ResolveCapabilities(TelegramMediaKind.Unknown);

        Assert.Empty(caps);
    }
}
