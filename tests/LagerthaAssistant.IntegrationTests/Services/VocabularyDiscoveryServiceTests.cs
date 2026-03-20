namespace LagerthaAssistant.IntegrationTests.Services;

using System.Net;
using System.Net.Http;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Api.Services;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Domain.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class VocabularyDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverAsync_ShouldNormalizePluralNounsToSingular()
    {
        var ai = new FakeAiChatClient(
            """
            [
              {"word":"cases","pos":"n"},
              {"word":"developers","pos":"n"},
              {"word":"issues","pos":"n"}
            ]
            """);
        var wordValidation = new FakeWordValidationService(
            [
                "cases", "case",
                "developers", "developer",
                "issues", "issue"
            ]);
        var sut = CreateSut(ai, new FakeVocabularyIndexService(), wordValidation);

        var result = await sut.DiscoverAsync("cases developers issues", CancellationToken.None);

        Assert.Equal(VocabularyDiscoveryStatus.Success, result.Status);
        Assert.Contains(result.Candidates, item => item.Word == "case" && item.PartOfSpeech == "n");
        Assert.Contains(result.Candidates, item => item.Word == "developer" && item.PartOfSpeech == "n");
        Assert.Contains(result.Candidates, item => item.Word == "issue" && item.PartOfSpeech == "n");
        Assert.DoesNotContain(result.Candidates, item => item.Word == "cases");
        Assert.DoesNotContain(result.Candidates, item => item.Word == "developers");
        Assert.DoesNotContain(result.Candidates, item => item.Word == "issues");
    }

    [Fact]
    public async Task DiscoverAsync_ShouldKeepSingularNounThatEndsWithS()
    {
        var ai = new FakeAiChatClient(
            """
            [
              {"word":"business","pos":"n"}
            ]
            """);
        var wordValidation = new FakeWordValidationService(["business", "busines"]);
        var sut = CreateSut(ai, new FakeVocabularyIndexService(), wordValidation);

        var result = await sut.DiscoverAsync("business", CancellationToken.None);

        Assert.Equal(VocabularyDiscoveryStatus.Success, result.Status);
        Assert.Contains(result.Candidates, item => item.Word == "business" && item.PartOfSpeech == "n");
        Assert.DoesNotContain(result.Candidates, item => item.Word == "busines");
    }

    private static VocabularyDiscoveryService CreateSut(
        IAiChatClient aiChatClient,
        IVocabularyIndexService vocabularyIndexService,
        IWordValidationService wordValidationService)
    {
        return new VocabularyDiscoveryService(
            new FakeHttpClientFactory(),
            aiChatClient,
            vocabularyIndexService,
            wordValidationService,
            NullLogger<VocabularyDiscoveryService>.Instance);
    }

    private sealed class FakeAiChatClient(string content) : IAiChatClient
    {
        private readonly string _content = content;

        public Task<AssistantCompletionResult> CompleteAsync(
            IReadOnlyCollection<ConversationMessage> messages,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new AssistantCompletionResult(_content, "test-model", Usage: null));
    }

    private sealed class FakeWordValidationService(IReadOnlyCollection<string> words) : IWordValidationService
    {
        private readonly HashSet<string> _words = new(words, StringComparer.OrdinalIgnoreCase);

        public bool IsValidWord(string word) => _words.Contains(word);

        public IReadOnlyList<string> GetSuggestions(string word, int maxCount = 5) => [];
    }

    private sealed class FakeVocabularyIndexService : IVocabularyIndexService
    {
        public Task<VocabularyLookupResult> FindByInputAsync(string input, CancellationToken cancellationToken = default)
            => Task.FromResult(new VocabularyLookupResult(input, []));

        public Task<IReadOnlyDictionary<string, VocabularyLookupResult>> FindByInputsAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken = default)
        {
            var result = inputs.ToDictionary(
                input => input,
                input => new VocabularyLookupResult(input, []),
                StringComparer.OrdinalIgnoreCase);
            return Task.FromResult<IReadOnlyDictionary<string, VocabularyLookupResult>>(result);
        }

        public Task IndexLookupResultAsync(VocabularyLookupResult lookup, VocabularyStorageMode storageMode, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task HandleAppendResultAsync(
            string requestedWord,
            string assistantReply,
            string? targetDeckFileName,
            string? overridePartOfSpeech,
            VocabularyAppendResult appendResult,
            VocabularyStorageMode storageMode,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> ClearAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<int> RebuildAsync(IReadOnlyList<VocabularyDeckEntry> entries, VocabularyStorageMode storageMode, CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        private static readonly HttpClient Client = new(new FakeHttpMessageHandler())
        {
            BaseAddress = new Uri("https://example.com")
        };

        public HttpClient CreateClient(string name) => Client;
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body>sample</body></html>")
            };
            return Task.FromResult(response);
        }
    }
}
