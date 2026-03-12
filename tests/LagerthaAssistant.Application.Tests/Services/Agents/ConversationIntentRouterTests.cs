namespace LagerthaAssistant.Application.Tests.Services.Agents;

using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Services.Agents;
using Xunit;

public sealed class ConversationIntentRouterTests
{
    [Fact]
    public void TryResolve_ShouldParseSlashHistory()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("/history", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.History, intent.Type);
    }

    [Fact]
    public void TryResolve_ShouldParseNaturalHistory()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("show conversation history", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.History, intent.Type);
    }

    [Fact]
    public void TryResolve_ShouldNotHijackSingleWordVocabularyInput()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("history", out var intent);

        Assert.False(handled);
        Assert.Equal(ConversationCommandIntentType.Unsupported, intent.Type);
    }

    [Fact]
    public void TryResolve_ShouldParseNaturalSyncRunWithNumber()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("please run sync 15 now", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.SyncRun, intent.Type);
        Assert.Equal(15, intent.Number);
    }

    [Fact]
    public void TryResolve_ShouldParseSlashPromptHistory()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("/prompt history", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.PromptHistory, intent.Type);
    }

    [Fact]
    public void TryResolve_ShouldParseSlashPromptProposals()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("/prompt proposals", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.PromptProposals, intent.Type);
    }

    [Fact]
    public void TryResolve_ShouldMarkUnknownSlashAsUnsupportedCommandIntent()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("/exit", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.Unsupported, intent.Type);
    }
}