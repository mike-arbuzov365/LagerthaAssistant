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
        var sut = new VocabularyController(workflow, persistence, new FakeVocabularyBatchInputService(), new FakeVocabularyDeckService(), new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), scopeAccessor);

        var response = await sut.Analyze(new VocabularyAnalyzeRequest("   "), cancellationToken: CancellationToken.None);

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
        var sut = new VocabularyController(workflow, persistence, new FakeVocabularyBatchInputService(), new FakeVocabularyDeckService(), new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), scopeAccessor);

        var response = await sut.Analyze(
            new VocabularyAnalyzeRequest(
                "void",
                "  TeLeGrAm  ",
                "Mike",
                "chat-42",
                "wm-nouns-ua-en.xlsx",
                "n"),
            cancellationToken: CancellationToken.None);

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
        var sut = new VocabularyController(workflow, persistence, new FakeVocabularyBatchInputService(), new FakeVocabularyDeckService(), new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), scopeAccessor);

        var response = await sut.AnalyzeBatch(new VocabularyAnalyzeBatchRequest([]), cancellationToken: CancellationToken.None);

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
        var sut = new VocabularyController(workflow, persistence, new FakeVocabularyBatchInputService(), new FakeVocabularyDeckService(), new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), scopeAccessor);

        var response = await sut.AnalyzeBatch(
            new VocabularyAnalyzeBatchRequest(["  void  ", " ", "call back"]),
            cancellationToken: CancellationToken.None);

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
        var sut = new VocabularyController(workflow, persistence, batchInput, new FakeVocabularyDeckService(), new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), scopeAccessor);

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
        var sut = new VocabularyController(workflow, persistence, batchInput, new FakeVocabularyDeckService(), new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), scopeAccessor);

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
    public async Task GetStorageMode_ShouldReturnCurrentMode_AndSupportedModes()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var persistence = new FakeVocabularyPersistenceService();
        var batchInput = new FakeVocabularyBatchInputService();
        var storageModeProvider = new FakeVocabularyStorageModeProvider
        {
            CurrentMode = VocabularyStorageMode.Graph
        };
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new VocabularyController(workflow, persistence, batchInput, new FakeVocabularyDeckService(), storageModeProvider, new FakeVocabularyStoragePreferenceService(), scopeAccessor);

        var response = await sut.GetStorageMode(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<VocabularyStorageModeResponse>(ok.Value);

        Assert.Equal("graph", payload.Mode);
        Assert.Equal(["local", "graph"], payload.AvailableModes);
    }

    [Fact]
    public async Task SetStorageMode_ShouldReturnBadRequest_WhenModeMissing()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var persistence = new FakeVocabularyPersistenceService();
        var batchInput = new FakeVocabularyBatchInputService();
        var storageModeProvider = new FakeVocabularyStorageModeProvider();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new VocabularyController(workflow, persistence, batchInput, new FakeVocabularyDeckService(), storageModeProvider, new FakeVocabularyStoragePreferenceService(), scopeAccessor);

        var response = await sut.SetStorageMode(new VocabularySetStorageModeRequest(" "), cancellationToken: CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal(VocabularyStorageMode.Local, storageModeProvider.CurrentMode);
    }

    [Fact]
    public async Task SetStorageMode_ShouldReturnBadRequest_WhenModeUnsupported()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var persistence = new FakeVocabularyPersistenceService();
        var batchInput = new FakeVocabularyBatchInputService();
        var storageModeProvider = new FakeVocabularyStorageModeProvider();
        var storagePreferenceService = new FakeVocabularyStoragePreferenceService
        {
            SupportedModes = ["local", "graph", "hybrid"]
        };
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new VocabularyController(workflow, persistence, batchInput, new FakeVocabularyDeckService(), storageModeProvider, storagePreferenceService, scopeAccessor);

        var response = await sut.SetStorageMode(new VocabularySetStorageModeRequest("cloud"), cancellationToken: CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal("Unsupported mode 'cloud'. Use one of: local, graph, hybrid.", badRequest.Value);
        Assert.Equal(VocabularyStorageMode.Local, storageModeProvider.CurrentMode);
    }

    [Fact]
    public async Task SetStorageMode_ShouldUpdateMode_WhenValid()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var persistence = new FakeVocabularyPersistenceService();
        var batchInput = new FakeVocabularyBatchInputService();
        var storageModeProvider = new FakeVocabularyStorageModeProvider();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new VocabularyController(workflow, persistence, batchInput, new FakeVocabularyDeckService(), storageModeProvider, new FakeVocabularyStoragePreferenceService(), scopeAccessor);

        var response = await sut.SetStorageMode(new VocabularySetStorageModeRequest("graph"), cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<VocabularyStorageModeResponse>(ok.Value);

        Assert.Equal("graph", payload.Mode);
        Assert.Equal(VocabularyStorageMode.Graph, storageModeProvider.CurrentMode);
        Assert.Equal(["local", "graph"], payload.AvailableModes);
    }

    [Fact]
    public async Task GetDecks_ShouldReturnSortedDecks_WithSuggestedMarkers()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var persistence = new FakeVocabularyPersistenceService();
        var batchInput = new FakeVocabularyBatchInputService();
        var deckService = new FakeVocabularyDeckService
        {
            WritableDecks =
            [
                new VocabularyDeckFile("wm-verbs-us-en.xlsx", @"C:\Decks\wm-verbs-us-en.xlsx"),
                new VocabularyDeckFile("wm-persistant-expressions-ua-en.xlsx", @"C:\Decks\wm-persistant-expressions-ua-en.xlsx"),
                new VocabularyDeckFile("wm-irregular-verbs-ua-en.xlsx", @"C:\Decks\wm-irregular-verbs-ua-en.xlsx"),
                new VocabularyDeckFile("wm-nouns-ua-en.xlsx", @"C:\Decks\wm-nouns-ua-en.xlsx")
            ]
        };
        var storageModeProvider = new FakeVocabularyStorageModeProvider
        {
            CurrentMode = VocabularyStorageMode.Graph
        };
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new VocabularyController(workflow, persistence, batchInput, deckService, storageModeProvider, new FakeVocabularyStoragePreferenceService(), scopeAccessor);

        var response = await sut.GetDecks(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<VocabularyDeckCatalogResponse>(ok.Value);

        Assert.Equal("graph", payload.StorageMode);
        Assert.Equal(4, payload.Decks.Count);

        Assert.Equal("wm-irregular-verbs-ua-en.xlsx", payload.Decks[0].FileName);
        Assert.Equal("iv", payload.Decks[0].SuggestedPartOfSpeech);

        Assert.Equal("wm-nouns-ua-en.xlsx", payload.Decks[1].FileName);
        Assert.Equal("n", payload.Decks[1].SuggestedPartOfSpeech);

        Assert.Equal("wm-persistant-expressions-ua-en.xlsx", payload.Decks[2].FileName);
        Assert.Equal("pe", payload.Decks[2].SuggestedPartOfSpeech);

        Assert.Equal("wm-verbs-us-en.xlsx", payload.Decks[3].FileName);
        Assert.Equal("v", payload.Decks[3].SuggestedPartOfSpeech);
    }
    [Fact]
    public void GetPartOfSpeechMarkers_ShouldReturnOrderedMarkerCatalog()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var persistence = new FakeVocabularyPersistenceService();
        var batchInput = new FakeVocabularyBatchInputService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new VocabularyController(workflow, persistence, batchInput, new FakeVocabularyDeckService(), new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), scopeAccessor);

        var response = sut.GetPartOfSpeechMarkers();

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<VocabularyPartOfSpeechCatalogResponse>(ok.Value);

        Assert.Equal(10, payload.Markers.Count);

        Assert.Equal(1, payload.Markers[0].Number);
        Assert.Equal("n", payload.Markers[0].Marker);
        Assert.Equal("noun", payload.Markers[0].Label);

        Assert.Equal(4, payload.Markers[3].Number);
        Assert.Equal("pv", payload.Markers[3].Marker);
        Assert.Equal("phrasal verb", payload.Markers[3].Label);

        Assert.Equal(10, payload.Markers[^1].Number);
        Assert.Equal("pe", payload.Markers[^1].Marker);
        Assert.Equal("persistent expression", payload.Markers[^1].Label);
    }
    [Fact]
    public async Task SaveBatch_ShouldReturnBadRequest_WhenItemsMissing()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var persistence = new FakeVocabularyPersistenceService();
        var scopeAccessor = new FakeConversationScopeAccessor();
        var sut = new VocabularyController(workflow, persistence, new FakeVocabularyBatchInputService(), new FakeVocabularyDeckService(), new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), scopeAccessor);

        var response = await sut.SaveBatch(new VocabularySaveBatchRequest([]), cancellationToken: CancellationToken.None);

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
        var sut = new VocabularyController(workflow, persistence, new FakeVocabularyBatchInputService(), new FakeVocabularyDeckService(), new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), scopeAccessor);

        var response = await sut.SaveBatch(
            new VocabularySaveBatchRequest(
            [
                new VocabularySaveBatchItemRequest("void", "reply-1", "wm-nouns-ua-en.xlsx", "n"),
                new VocabularySaveBatchItemRequest("prepare", "reply-2", "wm-verbs-us-en.xlsx", "v"),
                new VocabularySaveBatchItemRequest("call back", "reply-3", "wm-phrasal-verbs-ua-en.xlsx", "pv")
            ]),
            cancellationToken: CancellationToken.None);

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
        var sut = new VocabularyController(workflow, persistence, new FakeVocabularyBatchInputService(), new FakeVocabularyDeckService(), new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), scopeAccessor);

        var response = await sut.Save(
            new VocabularySaveRequest("   ", "assistant reply"),
            cancellationToken: CancellationToken.None);

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
        var sut = new VocabularyController(workflow, persistence, new FakeVocabularyBatchInputService(), new FakeVocabularyDeckService(), new FakeVocabularyStorageModeProvider(), new FakeVocabularyStoragePreferenceService(), scopeAccessor);

        var response = await sut.Save(
            new VocabularySaveRequest(
                "void",
                "void\n\n(n) emptiness",
                "wm-nouns-ua-en.xlsx",
                "n"),
            cancellationToken: CancellationToken.None);

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
    private sealed class FakeVocabularyDeckService : IVocabularyDeckService
    {
        public IReadOnlyList<VocabularyDeckFile> WritableDecks { get; set; } = [];

        public Task<VocabularyLookupResult> FindInWritableDecksAsync(string word, CancellationToken cancellationToken = default)
            => Task.FromResult(new VocabularyLookupResult(word, []));

        public Task<IReadOnlyList<VocabularyDeckFile>> GetWritableDeckFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(WritableDecks);

        public Task<VocabularyAppendPreviewResult> PreviewAppendFromAssistantReplyAsync(
            string requestedWord,
            string assistantReply,
            string? forcedDeckFileName = null,
            string? overridePartOfSpeech = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new VocabularyAppendPreviewResult(VocabularyAppendPreviewStatus.ParseFailed, requestedWord, Message: "not used"));

        public Task<VocabularyAppendResult> AppendFromAssistantReplyAsync(
            string requestedWord,
            string assistantReply,
            string? forcedDeckFileName = null,
            string? overridePartOfSpeech = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new VocabularyAppendResult(VocabularyAppendStatus.Error, Message: "not used"));
    }

    private sealed class FakeVocabularyStorageModeProvider : IVocabularyStorageModeProvider
    {
        public VocabularyStorageMode CurrentMode { get; set; } = VocabularyStorageMode.Local;

        public void SetMode(VocabularyStorageMode mode)
        {
            CurrentMode = mode;
        }

        public bool TryParse(string? value, out VocabularyStorageMode mode)
        {
            if (string.Equals(value, "local", StringComparison.OrdinalIgnoreCase))
            {
                mode = VocabularyStorageMode.Local;
                return true;
            }

            if (string.Equals(value, "graph", StringComparison.OrdinalIgnoreCase))
            {
                mode = VocabularyStorageMode.Graph;
                return true;
            }

            mode = default;
            return false;
        }

        public string ToText(VocabularyStorageMode mode)
            => mode.ToString().ToLowerInvariant();
    }
    private sealed class FakeVocabularyStoragePreferenceService : IVocabularyStoragePreferenceService
    {
        public IReadOnlyList<string> SupportedModes { get; set; } = ["local", "graph"];

        public VocabularyStorageMode CurrentMode { get; set; } = VocabularyStorageMode.Graph;

        public ConversationScope? LastGetScope { get; private set; }

        public ConversationScope? LastSetScope { get; private set; }

        public Task<VocabularyStorageMode> GetModeAsync(ConversationScope scope, CancellationToken cancellationToken = default)
        {
            LastGetScope = scope;
            return Task.FromResult(CurrentMode);
        }

        public Task<VocabularyStorageMode> SetModeAsync(
            ConversationScope scope,
            VocabularyStorageMode mode,
            CancellationToken cancellationToken = default)
        {
            LastSetScope = scope;
            CurrentMode = mode;
            return Task.FromResult(mode);
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
