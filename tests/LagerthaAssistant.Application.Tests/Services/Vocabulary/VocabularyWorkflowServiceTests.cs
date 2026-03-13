namespace LagerthaAssistant.Application.Tests.Services.Vocabulary;

using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;
using LagerthaAssistant.Domain.AI;
using LagerthaAssistant.Domain.Entities;
using Xunit;

public sealed class VocabularyWorkflowServiceTests
{
    [Fact]
    public async Task ProcessAsync_ShouldReturnFromSqlIndex_WhenIndexedCardExists()
    {
        var order = new List<string>();
        var assistant = new FakeAssistantSessionService(order);
        var deck = new FakeVocabularyDeckService(order);
        var index = new FakeVocabularyIndexService
        {
            LookupFactory = input => new VocabularyLookupResult(input,
            [
                new VocabularyDeckEntry("wm-nouns-ua-en.xlsx", "path", 7, input, "(n) indexed", "Indexed example")
            ])
        };

        var sut = new VocabularyWorkflowService(assistant, deck, index, new FakeStorageModeProvider());

        var result = await sut.ProcessAsync("void");

        Assert.True(result.FoundInDeck);
        Assert.Equal(0, assistant.AskCalls);
        Assert.Equal(0, deck.LookupCalls);
        Assert.Equal(0, index.IndexLookupCalls);
    }

    [Fact]
    public async Task ProcessAsync_ShouldSkipAssistant_WhenFoundInDeck()
    {
        var order = new List<string>();
        var assistant = new FakeAssistantSessionService(order);
        var deck = new FakeVocabularyDeckService(order)
        {
            LookupFactory = word => new VocabularyLookupResult(word,
            [
                new VocabularyDeckEntry("wm-nouns-ua-en.xlsx", "path", 42, word, "(n) test", "Example")
            ])
        };

        var index = new FakeVocabularyIndexService();
        var sut = new VocabularyWorkflowService(assistant, deck, index, new FakeStorageModeProvider());

        var result = await sut.ProcessAsync("void");

        Assert.True(result.FoundInDeck);
        Assert.Null(result.AssistantCompletion);
        Assert.Null(result.AppendPreview);
        Assert.Equal(0, assistant.AskCalls);
        Assert.Equal(0, deck.PreviewCalls);
        Assert.Equal(["lookup:void"], order);
    }

    [Fact]
    public async Task ProcessAsync_ShouldCallAssistantAndPreview_WhenNotFoundInDeck()
    {
        var order = new List<string>();
        var assistant = new FakeAssistantSessionService(order)
        {
            CompletionFactory = input => new AssistantCompletionResult($"{input}\n\n(v) test\n\nExample.", "test-model", null)
        };

        var deck = new FakeVocabularyDeckService(order)
        {
            LookupFactory = word => new VocabularyLookupResult(word, []),
            PreviewFactory = (word, _) => new VocabularyAppendPreviewResult(
                VocabularyAppendPreviewStatus.ReadyToAppend,
                word,
                "wm-verbs-us-en.xlsx",
                "C:/deck/wm-verbs-us-en.xlsx")
        };

        var index = new FakeVocabularyIndexService();
        var sut = new VocabularyWorkflowService(assistant, deck, index, new FakeStorageModeProvider());

        var result = await sut.ProcessAsync("prepare");

        Assert.False(result.FoundInDeck);
        Assert.NotNull(result.AssistantCompletion);
        Assert.NotNull(result.AppendPreview);
        Assert.Equal(1, assistant.AskCalls);
        Assert.Equal(1, deck.PreviewCalls);
        Assert.Equal(["lookup:prepare", "ask:prepare", "preview:prepare"], order);
    }

    [Fact]
    public async Task ProcessBatchAsync_ShouldProcessItemsSequentially()
    {
        var order = new List<string>();
        var assistant = new FakeAssistantSessionService(order)
        {
            CompletionFactory = input => new AssistantCompletionResult($"{input}\n\n(v) test\n\nExample.", "test-model", null)
        };

        var deck = new FakeVocabularyDeckService(order)
        {
            LookupFactory = word => new VocabularyLookupResult(word, []),
            PreviewFactory = (word, _) => new VocabularyAppendPreviewResult(
                VocabularyAppendPreviewStatus.ReadyToAppend,
                word,
                "wm-verbs-us-en.xlsx",
                "C:/deck/wm-verbs-us-en.xlsx")
        };

        var index = new FakeVocabularyIndexService();
        var sut = new VocabularyWorkflowService(assistant, deck, index, new FakeStorageModeProvider());

        var results = await sut.ProcessBatchAsync(["void", "prepare"]);

        Assert.Equal(2, results.Count);
        Assert.Equal(
            [
                "lookup:void",
                "ask:void",
                "preview:void",
                "lookup:prepare",
                "ask:prepare",
                "preview:prepare"
            ],
            order);
        Assert.Equal(1, index.BatchLookupCalls);
        Assert.Equal(0, index.SingleLookupCalls);
    }

    [Fact]
    public async Task ProcessBatchAsync_ShouldUseBulkIndexLookups_AndSkipPerItemIndexQueries()
    {
        var order = new List<string>();
        var assistant = new FakeAssistantSessionService(order)
        {
            CompletionFactory = input => new AssistantCompletionResult($"{input}\n\n(v) test\n\nExample.", "test-model", null)
        };

        var deck = new FakeVocabularyDeckService(order)
        {
            LookupFactory = word => new VocabularyLookupResult(word, []),
            PreviewFactory = (word, _) => new VocabularyAppendPreviewResult(
                VocabularyAppendPreviewStatus.ReadyToAppend,
                word,
                "wm-verbs-us-en.xlsx",
                "C:/deck/wm-verbs-us-en.xlsx")
        };

        var index = new FakeVocabularyIndexService
        {
            BatchLookupFactory = inputs =>
            {
                var result = inputs.ToDictionary(
                    input => input,
                    input => new VocabularyLookupResult(input, []),
                    StringComparer.OrdinalIgnoreCase);

                result["void"] = new VocabularyLookupResult("void",
                [
                    new VocabularyDeckEntry("wm-nouns-ua-en.xlsx", "path", 7, "void", "(n) indexed", "Indexed example")
                ]);

                return result;
            }
        };

        var sut = new VocabularyWorkflowService(assistant, deck, index, new FakeStorageModeProvider());

        var results = await sut.ProcessBatchAsync(["void", "prepare"]);

        Assert.Equal(2, results.Count);
        Assert.True(results[0].FoundInDeck);
        Assert.False(results[1].FoundInDeck);
        Assert.Equal(1, index.BatchLookupCalls);
        Assert.Equal(0, index.SingleLookupCalls);
        Assert.Equal(
            [
                "lookup:prepare",
                "ask:prepare",
                "preview:prepare"
            ],
            order);
    }

    [Fact]
    public async Task ProcessBatchAsync_ShouldReuseGeneratedResult_ForDuplicateInputs()
    {
        var order = new List<string>();
        var assistant = new FakeAssistantSessionService(order)
        {
            CompletionFactory = input => new AssistantCompletionResult($"{input}\n\n(v) test\n\nExample.", "test-model", null)
        };

        var deck = new FakeVocabularyDeckService(order)
        {
            LookupFactory = word => new VocabularyLookupResult(word, []),
            PreviewFactory = (word, _) => new VocabularyAppendPreviewResult(
                VocabularyAppendPreviewStatus.ReadyToAppend,
                word,
                "wm-verbs-us-en.xlsx",
                "C:/deck/wm-verbs-us-en.xlsx")
        };

        var index = new FakeVocabularyIndexService();
        var sut = new VocabularyWorkflowService(assistant, deck, index, new FakeStorageModeProvider());

        var results = await sut.ProcessBatchAsync(["void", "void"]);

        Assert.Equal(2, results.Count);
        Assert.False(results[0].FoundInDeck);
        Assert.False(results[1].FoundInDeck);
        Assert.Equal("void", results[0].Input);
        Assert.Equal("void", results[1].Input);
        Assert.Equal(1, assistant.AskCalls);
        Assert.Equal(1, deck.LookupCalls);
        Assert.Equal(1, deck.PreviewCalls);
        Assert.Equal(
            [
                "lookup:void",
                "ask:void",
                "preview:void"
            ],
            order);
    }

    [Fact]
    public async Task ProcessBatchAsync_ShouldPreserveOriginalInputText_ForCachedDuplicates()
    {
        var order = new List<string>();
        var assistant = new FakeAssistantSessionService(order)
        {
            CompletionFactory = input => new AssistantCompletionResult($"{input}\n\n(v) test\n\nExample.", "test-model", null)
        };

        var deck = new FakeVocabularyDeckService(order)
        {
            LookupFactory = word => new VocabularyLookupResult(word, []),
            PreviewFactory = (word, _) => new VocabularyAppendPreviewResult(
                VocabularyAppendPreviewStatus.ReadyToAppend,
                word,
                "wm-verbs-us-en.xlsx",
                "C:/deck/wm-verbs-us-en.xlsx")
        };

        var index = new FakeVocabularyIndexService();
        var sut = new VocabularyWorkflowService(assistant, deck, index, new FakeStorageModeProvider());

        var results = await sut.ProcessBatchAsync(["Void", "void"]);

        Assert.Equal(2, results.Count);
        Assert.Equal("Void", results[0].Input);
        Assert.Equal("Void", results[0].Lookup.Query);
        Assert.Equal("void", results[1].Input);
        Assert.Equal("void", results[1].Lookup.Query);
        Assert.Equal(1, assistant.AskCalls);
    }

    [Fact]
    public async Task ProcessBatchAsync_ShouldReturnEmpty_WhenAllInputsAreBlank()
    {
        var order = new List<string>();
        var assistant = new FakeAssistantSessionService(order);
        var deck = new FakeVocabularyDeckService(order);
        var index = new FakeVocabularyIndexService();
        var sut = new VocabularyWorkflowService(assistant, deck, index, new FakeStorageModeProvider());

        var results = await sut.ProcessBatchAsync([" ", "\t", ""]);

        Assert.Empty(results);
        Assert.Equal(0, index.BatchLookupCalls);
        Assert.Equal(0, deck.LookupCalls);
        Assert.Equal(0, assistant.AskCalls);
    }

    private sealed class FakeAssistantSessionService : IAssistantSessionService
    {
        private readonly List<string> _order;

        public FakeAssistantSessionService(List<string> order)
        {
            _order = order;
        }

        public int AskCalls { get; private set; }

        public Func<string, AssistantCompletionResult>? CompletionFactory { get; set; }

        public IReadOnlyCollection<ConversationMessage> Messages => [];

        public Task<AssistantCompletionResult> AskAsync(string userMessage, CancellationToken cancellationToken = default)
        {
            AskCalls++;
            _order.Add($"ask:{userMessage}");

            var completion = CompletionFactory?.Invoke(userMessage)
                ?? new AssistantCompletionResult("ok", "test-model", null);

            return Task.FromResult(completion);
        }

        public Task<IReadOnlyCollection<ConversationMessage>> GetRecentHistoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ConversationMessage>>([]);

        public Task<IReadOnlyCollection<UserMemoryEntry>> GetActiveMemoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<UserMemoryEntry>>([]);

        public Task<string> GetSystemPromptAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<IReadOnlyCollection<SystemPromptEntry>> GetSystemPromptHistoryAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<SystemPromptEntry>>([]);

        public Task<IReadOnlyCollection<SystemPromptProposal>> GetSystemPromptProposalsAsync(int take, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<SystemPromptProposal>>([]);

        public Task<SystemPromptProposal> CreateSystemPromptProposalAsync(string prompt, string reason, double confidence, string source = "manual", CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SystemPromptProposal> GenerateSystemPromptProposalAsync(string goal, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<string> ApplySystemPromptProposalAsync(int proposalId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RejectSystemPromptProposalAsync(int proposalId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<string> SetSystemPromptAsync(string prompt, string source = "manual", CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void Reset()
        {
        }
    }

    private sealed class FakeVocabularyDeckService : IVocabularyDeckService
    {
        private readonly List<string> _order;

        public FakeVocabularyDeckService(List<string> order)
        {
            _order = order;
        }

        public int LookupCalls { get; private set; }

        public int PreviewCalls { get; private set; }

        public Func<string, VocabularyLookupResult>? LookupFactory { get; set; }

        public Func<string, string, VocabularyAppendPreviewResult>? PreviewFactory { get; set; }

        public Task<VocabularyLookupResult> FindInWritableDecksAsync(string word, CancellationToken cancellationToken = default)
        {
            LookupCalls++;
            _order.Add($"lookup:{word}");
            return Task.FromResult(LookupFactory?.Invoke(word) ?? new VocabularyLookupResult(word, []));
        }

        public Task<IReadOnlyList<VocabularyDeckFile>> GetWritableDeckFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VocabularyDeckFile>>([]);

        public Task<VocabularyAppendPreviewResult> PreviewAppendFromAssistantReplyAsync(
            string requestedWord,
            string assistantReply,
            string? forcedDeckFileName = null,
            string? overridePartOfSpeech = null,
            CancellationToken cancellationToken = default)
        {
            PreviewCalls++;
            _order.Add($"preview:{requestedWord}");

            return Task.FromResult(PreviewFactory?.Invoke(requestedWord, assistantReply)
                ?? new VocabularyAppendPreviewResult(VocabularyAppendPreviewStatus.ParseFailed, requestedWord, Message: "not set"));
        }

        public Task<VocabularyAppendResult> AppendFromAssistantReplyAsync(
            string requestedWord,
            string assistantReply,
            string? forcedDeckFileName = null,
            string? overridePartOfSpeech = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new VocabularyAppendResult(VocabularyAppendStatus.Error, Message: "not used"));
        }
    }

    private sealed class FakeVocabularyIndexService : IVocabularyIndexService
    {
        public int IndexLookupCalls { get; private set; }

        public int SingleLookupCalls { get; private set; }

        public int BatchLookupCalls { get; private set; }

        public Func<string, VocabularyLookupResult>? LookupFactory { get; set; }

        public Func<IReadOnlyList<string>, IReadOnlyDictionary<string, VocabularyLookupResult>>? BatchLookupFactory { get; set; }

        public Task<VocabularyLookupResult> FindByInputAsync(string input, CancellationToken cancellationToken = default)
        {
            SingleLookupCalls++;
            return Task.FromResult(LookupFactory?.Invoke(input) ?? new VocabularyLookupResult(input, []));
        }

        public Task<IReadOnlyDictionary<string, VocabularyLookupResult>> FindByInputsAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken = default)
        {
            BatchLookupCalls++;

            if (BatchLookupFactory is not null)
            {
                return Task.FromResult(BatchLookupFactory(inputs));
            }

            var result = inputs
                .Where(input => !string.IsNullOrWhiteSpace(input))
                .Select(input => input.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    input => input,
                    input => new VocabularyLookupResult(input, []),
                    StringComparer.OrdinalIgnoreCase);

            return Task.FromResult<IReadOnlyDictionary<string, VocabularyLookupResult>>(result);
        }

        public Task IndexLookupResultAsync(VocabularyLookupResult lookup, VocabularyStorageMode storageMode, CancellationToken cancellationToken = default)
        {
            IndexLookupCalls++;
            return Task.CompletedTask;
        }

        public Task HandleAppendResultAsync(string requestedWord, string assistantReply, string? targetDeckFileName, string? overridePartOfSpeech, VocabularyAppendResult appendResult, VocabularyStorageMode storageMode, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeStorageModeProvider : IVocabularyStorageModeProvider
    {
        public VocabularyStorageMode CurrentMode => VocabularyStorageMode.Local;

        public void SetMode(VocabularyStorageMode mode)
        {
        }

        public bool TryParse(string? value, out VocabularyStorageMode mode)
        {
            mode = VocabularyStorageMode.Local;
            return true;
        }

        public string ToText(VocabularyStorageMode mode)
            => mode.ToString().ToLowerInvariant();
    }

}
