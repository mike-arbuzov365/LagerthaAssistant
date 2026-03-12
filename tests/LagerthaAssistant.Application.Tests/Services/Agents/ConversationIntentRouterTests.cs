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
    public void TryResolve_ShouldParseUkrainianHelpIntent()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("які команди доступні", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.Help, intent.Type);
    }

    [Fact]
    public void TryResolve_ShouldParseUkrainianMemoryIntent()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("покажи пам'ять", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.Memory, intent.Type);
    }

    [Fact]
    public void TryResolve_ShouldParseUkrainianPromptIntent()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("покажи промпт", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.PromptShow, intent.Type);
    }

    [Fact]
    public void TryResolve_ShouldParseUkrainianResetConversationIntent()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("скинь контекст розмови", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.ResetConversation, intent.Type);
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
    public void TryResolve_ShouldParseSlashPromptSetWithArgument()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("/prompt set Keep examples short", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.PromptSet, intent.Type);
        Assert.Equal("Keep examples short", intent.Argument);
    }

    [Fact]
    public void TryResolve_ShouldParseSlashPromptSetWithMultilineArgument()
    {
        var sut = new ConversationIntentRouter();
        var input = $"/prompt set line one{Environment.NewLine}line two";

        var handled = sut.TryResolve(input, out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.PromptSet, intent.Type);
        Assert.Equal($"line one{Environment.NewLine}line two", intent.Argument);
    }

    [Fact]
    public void TryResolve_ShouldParseSlashPromptProposeWithReasonAndPrompt()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("/prompt propose Too verbose || Keep replies concise", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.PromptPropose, intent.Type);
        Assert.Equal("Too verbose", intent.Argument);
        Assert.Equal("Keep replies concise", intent.Argument2);
    }

    [Fact]
    public void TryResolve_ShouldParseSlashPromptApplyWithId()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("/prompt apply 42", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.PromptApply, intent.Type);
        Assert.Equal(42, intent.Number);
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