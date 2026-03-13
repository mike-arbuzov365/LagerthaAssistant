namespace LagerthaAssistant.IntegrationTests.Services;

using System.Net;
using System.Net.Http;
using System.Text;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Infrastructure.Options;
using LagerthaAssistant.Infrastructure.Services.Vocabulary;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class NotionCardExportServiceTests
{
    [Fact]
    public async Task ExportAsync_ShouldCreatePage_WhenNoExistingPageFound()
    {
        using var handler = new StubHttpMessageHandler(
        [
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"results\":[]}", Encoding.UTF8, "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"page-new\"}", Encoding.UTF8, "application/json")
            }
        ]);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.notion.com/v1/")
        };

        var sut = new NotionCardExportService(
            BuildOptions(conflictMode: "update"),
            httpClient,
            NullLogger<NotionCardExportService>.Instance);

        var result = await sut.ExportAsync(BuildRequest(existingPageId: null));

        Assert.Equal(NotionCardExportOutcome.Created, result.Outcome);
        Assert.True(result.Succeeded);
        Assert.Equal("page-new", result.PageId);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ExportAsync_ShouldSkip_WhenConflictModeIsSkip_AndPageAlreadyExists()
    {
        using var handler = new StubHttpMessageHandler(
        [
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"results\":[{\"id\":\"page-existing\"}]}", Encoding.UTF8, "application/json")
            }
        ]);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.notion.com/v1/")
        };

        var sut = new NotionCardExportService(
            BuildOptions(conflictMode: "skip"),
            httpClient,
            NullLogger<NotionCardExportService>.Instance);

        var result = await sut.ExportAsync(BuildRequest(existingPageId: null));

        Assert.Equal(NotionCardExportOutcome.Skipped, result.Outcome);
        Assert.True(result.Succeeded);
        Assert.Equal("page-existing", result.PageId);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ExportAsync_ShouldUpdate_WhenPageAlreadyExists_AndConflictModeIsUpdate()
    {
        using var handler = new StubHttpMessageHandler(
        [
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"results\":[{\"id\":\"page-existing\"}]}", Encoding.UTF8, "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"page-existing\"}", Encoding.UTF8, "application/json")
            }
        ]);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.notion.com/v1/")
        };

        var sut = new NotionCardExportService(
            BuildOptions(conflictMode: "update"),
            httpClient,
            NullLogger<NotionCardExportService>.Instance);

        var result = await sut.ExportAsync(BuildRequest(existingPageId: null));

        Assert.Equal(NotionCardExportOutcome.Updated, result.Outcome);
        Assert.True(result.Succeeded);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ExportAsync_ShouldReturnRecoverableFailure_WhenNotionReturns429()
    {
        using var handler = new StubHttpMessageHandler(
        [
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"results\":[]}", Encoding.UTF8, "application/json")
            },
            new HttpResponseMessage((HttpStatusCode)429)
            {
                Content = new StringContent("{\"message\":\"rate limited\"}", Encoding.UTF8, "application/json")
            }
        ]);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.notion.com/v1/")
        };

        var sut = new NotionCardExportService(
            BuildOptions(conflictMode: "update"),
            httpClient,
            NullLogger<NotionCardExportService>.Instance);

        var result = await sut.ExportAsync(BuildRequest(existingPageId: null));

        Assert.Equal(NotionCardExportOutcome.Failed, result.Outcome);
        Assert.False(result.Succeeded);
        Assert.True(result.IsRecoverableFailure);
    }

    private static NotionOptions BuildOptions(string conflictMode)
    {
        return new NotionOptions
        {
            Enabled = true,
            ApiKey = "secret",
            DatabaseId = "db-1",
            ConflictMode = conflictMode,
            KeyPropertyName = "Key",
            WordPropertyName = "Word",
            MeaningPropertyName = "Meaning",
            ExamplesPropertyName = "Examples",
            PartOfSpeechPropertyName = "PartOfSpeech",
            DeckPropertyName = "DeckFile",
            StorageModePropertyName = "StorageMode",
            RowNumberPropertyName = "RowNumber",
            LastSeenPropertyName = "LastSeenAtUtc"
        };
    }

    private static NotionCardExportRequest BuildRequest(string? existingPageId)
    {
        return new NotionCardExportRequest(
            CardId: 101,
            IdentityKey: "void|wm-nouns-ua-en.xlsx|local",
            Word: "void",
            Meaning: "(n) emptiness",
            Examples: "The function returns void.",
            PartOfSpeechMarker: "n",
            DeckFileName: "wm-nouns-ua-en.xlsx",
            StorageMode: "local",
            RowNumber: 21,
            LastSeenAtUtc: DateTimeOffset.UtcNow,
            ExistingPageId: existingPageId);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public StubHttpMessageHandler(IEnumerable<HttpResponseMessage> responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (_responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("{\"message\":\"No response configured.\"}", Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }
}

