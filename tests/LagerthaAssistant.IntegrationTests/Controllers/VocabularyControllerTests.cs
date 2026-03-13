namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class VocabularyControllerTests
{
    [Fact]
    public async Task Analyze_ShouldReturnBadRequest_WhenInputMissing()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var persistence = new FakeVocabularyPersistenceService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new VocabularyController(workflow, persistence, new FakeVocabularyBatchInputService(), scopeAccessor);

        var response = await sut.Analyze(new VocabularyAnalyzeRequest("   "), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Null(workflow.LastSingleInput);
    }

    [Fact]
    public async Task Analyze_ShouldSetScope_AndForwardOptionalFields()
    {
        var workflow = new FakeVocabularyWorkflowService
        {
            NextSingleResult = BuildWorkflowResult("void", includeAssistant: true)
        };
        var persistence = new FakeVocabularyPersistenceService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new VocabularyController(workflow, persistence, new FakeVocabularyBatchInputService(), scopeAccessor);

        var response = await sut.Analyze(
            new VocabularyAnalyzeRequest(
                "void",
                "  TeLeGrAm  ",
                "Mike",
                "chat-42",
                "wm-nouns-ua-en.xlsx",
                "n"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<VocabularyWorkflowItemResponse>(ok.Value);

        Assert.Equal("void", workflow.LastSingleInput);
        Assert.Equal("wm-nouns-ua-en.xlsx", workflow.LastForcedDeckFileName);
        Assert.Equal("n", workflow.LastOverridePartOfSpeech);

        Assert.Equal("telegram", scopeAccessor.Current.Channel);
        Assert.Equal("mike", scopeAccessor.Current.UserId);
        Assert.Equal("chat-42", scopeAccessor.Current.ConversationId);

        Assert.False(payload.FoundInDeck);
        Assert.NotNull(payload.AssistantCompletion);
        Assert.NotNull(payload.AppendPreview);
        Assert.Equal("readytoappend", payload.AppendPreview!.Status);
    }

    [Fact]
    public async Task AnalyzeBatch_ShouldReturnBadRequest_WhenInputsMissing()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var persistence = new FakeVocabularyPersistenceService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new VocabularyController(workflow, persistence, new FakeVocabularyBatchInputService(), scopeAccessor);

        var response = await sut.AnalyzeBatch(new VocabularyAnalyzeBatchRequest([]), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Null(workflow.LastBatchInputs);
    }

    [Fact]
    public async Task AnalyzeBatch_ShouldTrimInputs_AndSetDefaultScope()
    {
        var workflow = new FakeVocabularyWorkflowService
        {
            NextBatchResults =
            [
                BuildWorkflowResult("void", includeAssistant: false),
                BuildWorkflowResult("call back", includeAssistant: true)
            ]
        };
        var persistence = new FakeVocabularyPersistenceService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new VocabularyController(workflow, persistence, new FakeVocabularyBatchInputService(), scopeAccessor);

        var response = await sut.AnalyzeBatch(
            new VocabularyAnalyzeBatchRequest(["  void  ", " ", "call back"]),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<VocabularyWorkflowItemResponse>>(ok.Value);

        Assert.Equal(["void", "call back"], workflow.LastBatchInputs);
        Assert.Equal("api", scopeAccessor.Current.Channel);
        Assert.Equal("anonymous", scopeAccessor.Current.UserId);
        Assert.Equal("default", scopeAccessor.Current.ConversationId);

        Assert.Equal(2, payload.Count);
    }

    [Fact]
    public void ParseBatch_ShouldReturnBadRequest_WhenInputMissing()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var persistence = new FakeVocabularyPersistenceService();
        var batchInput = new FakeVocabularyBatchInputService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new VocabularyController(workflow, persistence, batchInput, scopeAccessor);

        var response = sut.ParseBatch(new VocabularyParseBatchRequest("   "));

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Null(batchInput.LastRawInput);
    }

    [Fact]
    public void ParseBatch_ShouldReturnParsedItems_AndSpaceSplitHints()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var persistence = new FakeVocabularyPersistenceService();
        var batchInput = new FakeVocabularyBatchInputService
        {
            NextResult = new VocabularyBatchParseResult(
                ["void", "prepare"],
                true,
                ["void", "prepare"],
                "void prepare")
        };
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new VocabularyController(workflow, persistence, batchInput, scopeAccessor);

        var response = sut.ParseBatch(new VocabularyParseBatchRequest("void prepare", true));

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<VocabularyParseBatchResponse>(ok.Value);

        Assert.Equal(["void", "prepare"], payload.Items);
        Assert.True(payload.ShouldOfferSpaceSplit);
        Assert.Equal(["void", "prepare"], payload.SpaceSplitCandidates);
        Assert.Equal("void prepare", payload.SingleItemWithoutSeparators);

        Assert.Equal("void prepare", batchInput.LastRawInput);
        Assert.True(batchInput.LastApplySpaceSplit);
    }
    [Fact]
    public async Task SaveBatch_ShouldReturnBadRequest_WhenItemsMissing()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var persistence = new FakeVocabularyPersistenceService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new VocabularyController(workflow, persistence, new FakeVocabularyBatchInputService(), scopeAccessor);

        var response = await sut.SaveBatch(new VocabularySaveBatchRequest([]), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Empty(persistence.Calls);
    }

    [Fact]
    public async Task SaveBatch_ShouldReturnSummary_AndMappedItemResults()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var persistence = new FakeVocabularyPersistenceService();
        persistence.NextAppendResults.Enqueue(new VocabularyAppendResult(
            VocabularyAppendStatus.Added,
            new VocabularyDeckEntry(
                "wm-nouns-ua-en.xlsx",
                @"C:\Decks\wm-nouns-ua-en.xlsx",
                201,
                "void",
                "(n) emptiness",
                "The function returns void."),
            null,
            "Added"));
        persistence.NextAppendResults.Enqueue(new VocabularyAppendResult(
            VocabularyAppendStatus.DuplicateFound,
            null,
            [
                new VocabularyDeckEntry(
                    "wm-verbs-us-en.xlsx",
                    @"C:\Decks\wm-verbs-us-en.xlsx",
                    78,
                    "prepare",
                    "(v) get ready",
                    "We prepare release scripts.")
            ],
            "Duplicate"));
        persistence.NextAppendResults.Enqueue(new VocabularyAppendResult(
            VocabularyAppendStatus.Error,
            null,
            null,
            "Locked"));

        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new VocabularyController(workflow, persistence, new FakeVocabularyBatchInputService(), scopeAccessor);

        var response = await sut.SaveBatch(
            new VocabularySaveBatchRequest(
            [
                new VocabularySaveBatchItemRequest("void", "reply-1", "wm-nouns-ua-en.xlsx", "n"),
                new VocabularySaveBatchItemRequest("prepare", "reply-2", "wm-verbs-us-en.xlsx", "v"),
                new VocabularySaveBatchItemRequest("call back", "reply-3", "wm-phrasal-verbs-ua-en.xlsx", "pv")
            ]),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<VocabularySaveBatchResponse>(ok.Value);

        Assert.Equal(3, payload.Total);
        Assert.Equal(1, payload.Added);
        Assert.Equal(1, payload.Duplicates);
        Assert.Equal(1, payload.Failed);
        Assert.Equal(3, payload.Items.Count);

        Assert.Equal(1, payload.Items[0].Index);
        Assert.Equal("void", payload.Items[0].RequestedWord);
        Assert.Equal("added", payload.Items[0].Result.Status);

        Assert.Equal(2, payload.Items[1].Index);
        Assert.Equal("prepare", payload.Items[1].RequestedWord);
        Assert.Equal("duplicatefound", payload.Items[1].Result.Status);

        Assert.Equal(3, payload.Items[2].Index);
        Assert.Equal("call back", payload.Items[2].RequestedWord);
        Assert.Equal("error", payload.Items[2].Result.Status);

        Assert.Equal(3, persistence.Calls.Count);
        Assert.Equal("void", persistence.Calls[0].RequestedWord);
        Assert.Equal("prepare", persistence.Calls[1].RequestedWord);
        Assert.Equal("call back", persistence.Calls[2].RequestedWord);
    }
    [Fact]
    public async Task Save_ShouldReturnBadRequest_WhenRequestedWordMissing()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var persistence = new FakeVocabularyPersistenceService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new VocabularyController(workflow, persistence, new FakeVocabularyBatchInputService(), scopeAccessor);

        var response = await sut.Save(
            new VocabularySaveRequest("   ", "assistant reply"),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Null(persistence.LastRequestedWord);
    }

    [Fact]
    public async Task Save_ShouldReturnMappedAppendResult()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var persistence = new FakeVocabularyPersistenceService
        {
            NextAppendResult = new VocabularyAppendResult(
                VocabularyAppendStatus.Added,
                new VocabularyDeckEntry(
                    "wm-nouns-ua-en.xlsx",
                    @"C:\Decks\wm-nouns-ua-en.xlsx",
                    101,
                    "void",
                    "(n) emptiness",
                    "The function returns void."),
                null,
                "Added successfully")
        };
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new VocabularyController(workflow, persistence, new FakeVocabularyBatchInputService(), scopeAccessor);

        var response = await sut.Save(
            new VocabularySaveRequest(
                "void",
                "void\n\n(n) emptiness",
                "wm-nouns-ua-en.xlsx",
                "n"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<VocabularyAppendResultResponse>(ok.Value);

        Assert.Equal("added", payload.Status);
        Assert.NotNull(payload.Entry);
        Assert.Equal("void", payload.Entry!.Word);

        Assert.Equal("void", persistence.LastRequestedWord);
        Assert.Equal("wm-nouns-ua-en.xlsx", persistence.LastForcedDeckFileName);
        Assert.Equal("n", persistence.LastOverridePartOfSpeech);
    }

    private static VocabularyWorkflowItemResult BuildWorkflowResult(string input, bool includeAssistant)
    {
        var lookup = new VocabularyLookupResult(input, []);
        if (!includeAssistant)
        {
            return new VocabularyWorkflowItemResult(input, lookup);
        }

        var completion = new AssistantCompletionResult(
            "reply",
            "gpt-4.1-mini",
            new AssistantUsage(10, 5, 15));

        var preview = new VocabularyAppendPreviewResult(
            VocabularyAppendPreviewStatus.ReadyToAppend,
            input,
            "wm-nouns-ua-en.xlsx",
            @"C:\Decks\wm-nouns-ua-en.xlsx",
            null,
            null);

        return new VocabularyWorkflowItemResult(input, lookup, completion, preview);
    }

    private sealed class FakeConversationScopeAccessor : IConversationScopeAccessor
    {
        public ConversationScope Current { get; private set; } = ConversationScope.Default;

        public void Set(ConversationScope scope)
        {
            Current = scope;
        }
    }

    private sealed class FakeVocabularyWorkflowService : IVocabularyWorkflowService
    {
        public VocabularyWorkflowItemResult NextSingleResult { get; set; } = BuildWorkflowResult("void", includeAssistant: false);

        public IReadOnlyList<VocabularyWorkflowItemResult> NextBatchResults { get; set; } = [];

        public string? LastSingleInput { get; private set; }

        public string? LastForcedDeckFileName { get; private set; }

        public string? LastOverridePartOfSpeech { get; private set; }

        public IReadOnlyList<string>? LastBatchInputs { get; private set; }

        public Task<VocabularyWorkflowItemResult> ProcessAsync(
            string input,
            string? forcedDeckFileName = null,
            string? overridePartOfSpeech = null,
            CancellationToken cancellationToken = default)
        {
            LastSingleInput = input;
            LastForcedDeckFileName = forcedDeckFileName;
            LastOverridePartOfSpeech = overridePartOfSpeech;
            return Task.FromResult(NextSingleResult);
        }

        public Task<IReadOnlyList<VocabularyWorkflowItemResult>> ProcessBatchAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken = default)
        {
            LastBatchInputs = inputs.ToList();

            if (NextBatchResults.Count > 0)
            {
                return Task.FromResult(NextBatchResults);
            }

            return Task.FromResult<IReadOnlyList<VocabularyWorkflowItemResult>>(
                inputs.Select(input => BuildWorkflowResult(input, includeAssistant: false)).ToList());
        }
    }


    private sealed class FakeVocabularyBatchInputService : IVocabularyBatchInputService
    {
        public VocabularyBatchParseResult NextResult { get; set; } = new([], false, [], null);

        public string? LastRawInput { get; private set; }

        public bool LastApplySpaceSplit { get; private set; }

        public VocabularyBatchParseResult Parse(string rawInput, bool applySpaceSplitForSingleItem = false)
        {
            LastRawInput = rawInput;
            LastApplySpaceSplit = applySpaceSplitForSingleItem;
            return NextResult;
        }
    }
    private sealed class FakeVocabularyPersistenceService : IVocabularyPersistenceService
    {
        public VocabularyAppendResult NextAppendResult { get; set; } = new(VocabularyAppendStatus.Added);

        public Queue<VocabularyAppendResult> NextAppendResults { get; } = [];

        public List<(string RequestedWord, string AssistantReply, string? ForcedDeckFileName, string? OverridePartOfSpeech)> Calls { get; } = [];

        public string? LastRequestedWord { get; private set; }

        public string? LastAssistantReply { get; private set; }

        public string? LastForcedDeckFileName { get; private set; }

        public string? LastOverridePartOfSpeech { get; private set; }

        public Task<VocabularyAppendResult> AppendFromAssistantReplyAsync(
            string requestedWord,
            string assistantReply,
            string? forcedDeckFileName = null,
            string? overridePartOfSpeech = null,
            CancellationToken cancellationToken = default)
        {
            LastRequestedWord = requestedWord;
            LastAssistantReply = assistantReply;
            LastForcedDeckFileName = forcedDeckFileName;
            LastOverridePartOfSpeech = overridePartOfSpeech;

            Calls.Add((requestedWord, assistantReply, forcedDeckFileName, overridePartOfSpeech));

            if (NextAppendResults.Count > 0)
            {
                return Task.FromResult(NextAppendResults.Dequeue());
            }

            return Task.FromResult(NextAppendResult);
        }
    }
}
