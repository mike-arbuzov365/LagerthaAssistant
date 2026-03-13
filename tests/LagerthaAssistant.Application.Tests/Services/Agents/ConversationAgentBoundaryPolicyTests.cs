namespace LagerthaAssistant.Application.Tests.Services.Agents;

using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Services.Agents;
using Xunit;

public sealed class ConversationAgentBoundaryPolicyTests
{
    [Fact]
    public void IsAllowed_ShouldBlockVocabularyAgent_ForSlashInput()
    {
        var sut = new ConversationAgentBoundaryPolicy();
        var agent = new FakeVocabularyAgent();
        var context = new ConversationAgentContext("/sync status", ["/sync status"], ConversationScope.Default, new ConversationCommandIntent(ConversationCommandIntentType.SyncStatus));

        var allowed = sut.IsAllowed(agent, context, context.ResolvedIntent!, out var reason);

        Assert.False(allowed);
        Assert.Equal("slash-not-supported", reason);
    }

    [Fact]
    public void IsAllowed_ShouldBlockCommandAgent_ForBatchInput()
    {
        var sut = new ConversationAgentBoundaryPolicy();
        var agent = new FakeCommandAgent();
        var context = new ConversationAgentContext("void prepare", ["void", "prepare"], ConversationScope.Default, new ConversationCommandIntent(ConversationCommandIntentType.Unsupported));

        var allowed = sut.IsAllowed(agent, context, context.ResolvedIntent!, out var reason);

        Assert.False(allowed);
        Assert.Equal("batch-not-supported", reason);
    }

    [Fact]
    public void IsAllowed_ShouldBlockVocabularyAgent_ForDetectedCommandIntent()
    {
        var sut = new ConversationAgentBoundaryPolicy();
        var agent = new FakeVocabularyAgent();
        var context = new ConversationAgentContext("show history now", ["show history now"], ConversationScope.Default, new ConversationCommandIntent(ConversationCommandIntentType.History));

        var allowed = sut.IsAllowed(agent, context, context.ResolvedIntent!, out var reason);

        Assert.False(allowed);
        Assert.Equal("command-intent-boundary", reason);
    }

    [Fact]
    public void IsAllowed_ShouldAllowUnprofiledAgent()
    {
        var sut = new ConversationAgentBoundaryPolicy();
        var agent = new FakeUnprofiledAgent();
        var context = new ConversationAgentContext("/help", ["/help"], ConversationScope.Default, new ConversationCommandIntent(ConversationCommandIntentType.Help));

        var allowed = sut.IsAllowed(agent, context, context.ResolvedIntent!, out var reason);

        Assert.True(allowed);
        Assert.Equal(string.Empty, reason);
    }

    private sealed class FakeCommandAgent : IConversationAgent, IConversationAgentProfile
    {
        public string Name => "command-agent";

        public int Order => 10;

        public ConversationAgentRole Role => ConversationAgentRole.Command;

        public bool SupportsSlashCommands => true;

        public bool SupportsBatchInputs => false;

        public bool CanHandle(ConversationAgentContext context)
            => true;

        public Task<ConversationAgentResult> HandleAsync(ConversationAgentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(ConversationAgentResult.Empty(Name, "command"));
    }

    private sealed class FakeVocabularyAgent : IConversationAgent, IConversationAgentProfile
    {
        public string Name => "vocabulary-agent";

        public int Order => 100;

        public ConversationAgentRole Role => ConversationAgentRole.Vocabulary;

        public bool SupportsSlashCommands => false;

        public bool SupportsBatchInputs => true;

        public bool CanHandle(ConversationAgentContext context)
            => true;

        public Task<ConversationAgentResult> HandleAsync(ConversationAgentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(ConversationAgentResult.Empty(Name, "vocabulary"));
    }

    private sealed class FakeUnprofiledAgent : IConversationAgent
    {
        public string Name => "custom-agent";

        public int Order => 5;

        public bool CanHandle(ConversationAgentContext context)
            => true;

        public Task<ConversationAgentResult> HandleAsync(ConversationAgentContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(ConversationAgentResult.Empty(Name, "custom"));
    }
}
