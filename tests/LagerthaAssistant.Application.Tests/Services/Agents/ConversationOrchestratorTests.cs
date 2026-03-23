namespace LagerthaAssistant.Application.Tests.Services.Agents;

using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Agents;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public sealed class ConversationOrchestratorTests
{
    [Fact]
    public async Task ProcessAsync_ShouldUseCommandAgent_ForSlashInput()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var agents = new IConversationAgent[]
        {
            new FakeSlashCommandAgent(),
            new VocabularyConversationAgent(workflow)
        };

        var sut = new ConversationOrchestrator(
            agents,
            NullLogger<ConversationOrchestrator>.Instance,
            new FakeConversationScopeAccessor());

        var result = await sut.ProcessAsync("/help");

        Assert.Equal("command-agent", result.AgentName);
        Assert.Equal("command", result.Intent);
        Assert.Empty(result.Items);
        Assert.Equal(0, workflow.SingleCalls);
        Assert.Equal(0, workflow.BatchCalls);
    }

    [Fact]
    public async Task ProcessAsync_ShouldUseVocabularyAgent_ForRegularInput()
    {
        var workflow = new FakeVocabularyWorkflowService
        {
            SingleFactory = input => BuildSingle(input)
        };

        var agents = new IConversationAgent[]
        {
            new FakeSlashCommandAgent(),
            new VocabularyConversationAgent(workflow)
        };

        var sut = new ConversationOrchestrator(
            agents,
            NullLogger<ConversationOrchestrator>.Instance,
            new FakeConversationScopeAccessor());

        var result = await sut.ProcessAsync("void");

        Assert.Equal("vocabulary-agent", result.AgentName);
        Assert.Equal("vocabulary.single", result.Intent);
        Assert.Single(result.Items);
        Assert.Equal(1, workflow.SingleCalls);
        Assert.Equal(0, workflow.BatchCalls);
    }

    [Fact]
    public async Task ProcessAsync_ShouldTrackMetrics_WithProvidedChannel()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var metrics = new FakeConversationMetricsService();

        var agents = new IConversationAgent[]
        {
            new FakeSlashCommandAgent(),
            new VocabularyConversationAgent(workflow)
        };

        var sut = new ConversationOrchestrator(
            agents,
            NullLogger<ConversationOrchestrator>.Instance,
            new FakeConversationScopeAccessor(),
            metrics);

        var result = await sut.ProcessAsync("void", "ui");

        Assert.Equal("vocabulary-agent", result.AgentName);
        var tracked = Assert.Single(metrics.Tracked);
        Assert.Equal("ui", tracked.Channel);
        Assert.Equal("vocabulary.single", tracked.Result.Intent);
    }

    [Fact]
    public async Task ProcessAsync_DefaultOverload_ShouldTrackMetrics_WithUnknownChannel()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var metrics = new FakeConversationMetricsService();

        var agents = new IConversationAgent[]
        {
            new FakeSlashCommandAgent(),
            new VocabularyConversationAgent(workflow)
        };

        var sut = new ConversationOrchestrator(
            agents,
            NullLogger<ConversationOrchestrator>.Instance,
            new FakeConversationScopeAccessor(),
            metrics);

        _ = await sut.ProcessAsync("void");

        var tracked = Assert.Single(metrics.Tracked);
        Assert.Equal("unknown", tracked.Channel);
    }

    [Fact]
    public async Task ProcessAsync_ShouldSetScopeAccessor_WhenUserAndConversationProvided()
    {
        var workflow = new FakeVocabularyWorkflowService();
        var scopeAccessor = new FakeConversationScopeAccessor();

        var agents = new IConversationAgent[]
        {
            new FakeSlashCommandAgent(),
            new VocabularyConversationAgent(workflow)
        };

        var sut = new ConversationOrchestrator(
            agents,
            NullLogger<ConversationOrchestrator>.Instance,
            scopeAccessor);

        _ = await sut.ProcessAsync("void", "api", "Mike", "chat-42");

        Assert.Equal("api", scopeAccessor.Current.Channel);
        Assert.Equal("mike", scopeAccessor.Current.UserId);
        Assert.Equal("chat-42", scopeAccessor.Current.ConversationId);
    }

    [Fact]
    public async Task ProcessAsync_CanonicalOverload_ShouldPropagateLocaleAndScopeIntoAgentContext()
    {
        var capturingAgent = new CapturingAgent();
        var sut = new ConversationOrchestrator(
            [capturingAgent],
            NullLogger<ConversationOrchestrator>.Instance,
            new FakeConversationScopeAccessor());

        var result = await sut.ProcessAsync(
            "void",
            "telegram",
            "uk",
            "Mike",
            "chat-42",
            CancellationToken.None);

        Assert.Equal("capturing-agent", result.AgentName);
        Assert.NotNull(capturingAgent.LastContext);
        Assert.Equal("uk", capturingAgent.LastContext!.Locale);
        Assert.Equal("telegram", capturingAgent.LastContext.Scope.Channel);
        Assert.Equal("mike", capturingAgent.LastContext.Scope.UserId);
        Assert.Equal("chat-42", capturingAgent.LastContext.Scope.ConversationId);
    }

    [Fact]
    public async Task ProcessAsync_ShouldSkipVocabularyAgent_WhenCommandIntentDetectedByBoundaryPolicy()
    {
        var agents = new IConversationAgent[]
        {
            new FakeAlwaysVocabularyAgent(),
            new FakeAlwaysCommandAgent()
        };

        var sut = new ConversationOrchestrator(
            agents,
            NullLogger<ConversationOrchestrator>.Instance,
            new FakeConversationScopeAccessor(),
            metricsService: null,
            intentRouter: new ConversationIntentRouter(),
            boundaryPolicy: new ConversationAgentBoundaryPolicy());

        var result = await sut.ProcessAsync("show conversation history");

        Assert.Equal("command-agent", result.AgentName);
        Assert.Equal("command", result.Intent);
    }

    private static VocabularyWorkflowItemResult BuildSingle(string input)
    {
        var lookup = new VocabularyLookupResult(input, []);
        var completion = new AssistantCompletionResult($"{input}\n\n(n) test\n\nExample.", "test-model", null);
        var preview = new VocabularyAppendPreviewResult(
            VocabularyAppendPreviewStatus.ReadyToAppend,
            input,
            "wm-nouns-ua-en.xlsx",
            "C:/deck/wm-nouns-ua-en.xlsx");

        return new VocabularyWorkflowItemResult(input, lookup, completion, preview);
    }

    private sealed class FakeSlashCommandAgent : IConversationAgent
    {
        public string Name => "command-agent";

        public int Order => 10;

        public bool CanHandle(ConversationAgentContext context)
            => context.IsSlashCommand;

        public Task<ConversationAgentResult> HandleAsync(ConversationAgentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(ConversationAgentResult.Empty(Name, "command", "Slash command"));
    }

    private sealed class FakeAlwaysVocabularyAgent : IConversationAgent, IConversationAgentProfile
    {
        public string Name => "vocabulary-agent";

        public int Order => 1;

        public ConversationAgentRole Role => ConversationAgentRole.Vocabulary;

        public bool SupportsSlashCommands => false;

        public bool SupportsBatchInputs => true;

        public bool CanHandle(ConversationAgentContext context)
            => true;

        public Task<ConversationAgentResult> HandleAsync(ConversationAgentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(ConversationAgentResult.Empty(Name, "vocabulary"));
    }

    private sealed class FakeAlwaysCommandAgent : IConversationAgent, IConversationAgentProfile
    {
        public string Name => "command-agent";

        public int Order => 2;

        public ConversationAgentRole Role => ConversationAgentRole.Command;

        public bool SupportsSlashCommands => true;

        public bool SupportsBatchInputs => false;

        public bool CanHandle(ConversationAgentContext context)
            => true;

        public Task<ConversationAgentResult> HandleAsync(ConversationAgentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(ConversationAgentResult.Empty(Name, "command"));
    }

    private sealed class CapturingAgent : IConversationAgent
    {
        public string Name => "capturing-agent";

        public int Order => 1;

        public ConversationAgentContext? LastContext { get; private set; }

        public bool CanHandle(ConversationAgentContext context) => true;

        public Task<ConversationAgentResult> HandleAsync(ConversationAgentContext context, CancellationToken cancellationToken = default)
        {
            LastContext = context;
            return Task.FromResult(ConversationAgentResult.Empty(Name, "captured"));
        }
    }

    private sealed class FakeVocabularyWorkflowService : IVocabularyWorkflowService
    {
        public int SingleCalls { get; private set; }

        public int BatchCalls { get; private set; }

        public Func<string, VocabularyWorkflowItemResult>? SingleFactory { get; set; }

        public Func<IReadOnlyList<string>, IReadOnlyList<VocabularyWorkflowItemResult>>? BatchFactory { get; set; }

        public Task<VocabularyWorkflowItemResult> ProcessAsync(
            string input,
            string? forcedDeckFileName = null,
            string? overridePartOfSpeech = null,
            bool bypassValidation = false,
            CancellationToken cancellationToken = default)
        {
            SingleCalls++;
            return Task.FromResult(SingleFactory?.Invoke(input) ?? BuildSingle(input));
        }

        public Task<IReadOnlyList<VocabularyWorkflowItemResult>> ProcessBatchAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken = default)
        {
            BatchCalls++;
            var output = BatchFactory?.Invoke(inputs)
                ?? inputs.Select(BuildSingle).ToList();

            return Task.FromResult(output);
        }
    }

    private sealed class FakeConversationMetricsService : IConversationMetricsService
    {
        public List<TrackedRow> Tracked { get; } = [];

        public Task TrackAsync(string channel, ConversationAgentResult result, CancellationToken cancellationToken = default)
        {
            Tracked.Add(new TrackedRow(channel, result));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ConversationIntentMetricSummary>> GetTopIntentsAsync(
            int days,
            int take,
            string? channel = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ConversationIntentMetricSummary>>([]);

        public sealed record TrackedRow(string Channel, ConversationAgentResult Result);
    }

    private sealed class FakeConversationScopeAccessor : IConversationScopeAccessor
    {
        public ConversationScope Current { get; private set; } = ConversationScope.Default;

        public void Set(ConversationScope scope)
        {
            Current = scope;
        }
    }
}
