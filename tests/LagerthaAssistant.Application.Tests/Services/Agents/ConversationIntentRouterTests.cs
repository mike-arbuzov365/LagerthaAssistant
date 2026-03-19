namespace LagerthaAssistant.Application.Tests.Services.Agents;

using LagerthaAssistant.Application.Constants;
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

    public static TheoryData<string, ConversationCommandIntentType> CanonicalSlashCommands =>
        new()
        {
            { ConversationSlashCommands.Help, ConversationCommandIntentType.Help },
            { ConversationSlashCommands.History, ConversationCommandIntentType.History },
            { ConversationSlashCommands.Memory, ConversationCommandIntentType.Memory },
            { ConversationSlashCommands.Prompt, ConversationCommandIntentType.PromptShow },
            { ConversationSlashCommands.PromptDefault, ConversationCommandIntentType.PromptResetDefault },
            { ConversationSlashCommands.PromptHistory, ConversationCommandIntentType.PromptHistory },
            { ConversationSlashCommands.PromptProposals, ConversationCommandIntentType.PromptProposals },
            { ConversationSlashCommands.Sync, ConversationCommandIntentType.SyncStatus },
            { ConversationSlashCommands.SyncStatus, ConversationCommandIntentType.SyncStatus },
            { ConversationSlashCommands.SyncFailed, ConversationCommandIntentType.SyncFailed },
            { ConversationSlashCommands.SyncRun, ConversationCommandIntentType.SyncRun },
            { ConversationSlashCommands.SyncRetryFailed, ConversationCommandIntentType.SyncRetryFailed },
            { ConversationSlashCommands.Reset, ConversationCommandIntentType.ResetConversation },
            { ConversationSlashCommands.Index, ConversationCommandIntentType.IndexHelp },
            { ConversationSlashCommands.IndexClear, ConversationCommandIntentType.IndexClear },
            { ConversationSlashCommands.IndexRebuild, ConversationCommandIntentType.IndexRebuild }
        };

    [Theory]
    [MemberData(nameof(CanonicalSlashCommands))]
    public void TryResolve_ShouldParseCanonicalSlashCommands(string command, ConversationCommandIntentType expectedType)
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve(command, out var intent);

        Assert.True(handled);
        Assert.Equal(expectedType, intent.Type);

        if (expectedType == ConversationCommandIntentType.SyncRun)
        {
            Assert.Equal(ConversationCommandDefaults.SyncRunTake, intent.Number);
        }

        if (expectedType == ConversationCommandIntentType.SyncFailed)
        {
            Assert.Equal(ConversationCommandDefaults.SyncFailedPreviewTake, intent.Number);
        }

        if (expectedType == ConversationCommandIntentType.SyncRetryFailed)
        {
            Assert.Equal(ConversationCommandDefaults.SyncRetryFailedTake, intent.Number);
        }
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

        var handled = sut.TryResolve("\u044f\u043a\u0456 \u043a\u043e\u043c\u0430\u043d\u0434\u0438 \u0434\u043e\u0441\u0442\u0443\u043f\u043d\u0456", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.Help, intent.Type);
    }

    [Fact]
    public void TryResolve_ShouldParseUkrainianMemoryIntent()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("\u043f\u043e\u043a\u0430\u0436\u0438 \u043f\u0430\u043c'\u044f\u0442\u044c", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.Memory, intent.Type);
    }

    [Fact]
    public void TryResolve_ShouldParseUkrainianPromptIntent()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("\u043f\u043e\u043a\u0430\u0436\u0438 \u043f\u0440\u043e\u043c\u043f\u0442", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.PromptShow, intent.Type);
    }

    [Fact]
    public void TryResolve_ShouldParseUkrainianResetConversationIntent()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("\u0441\u043a\u0438\u043d\u044c \u043a\u043e\u043d\u0442\u0435\u043a\u0441\u0442 \u0440\u043e\u0437\u043c\u043e\u0432\u0438", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.ResetConversation, intent.Type);
    }

    [Fact]
    public void TryResolve_ShouldParseUkrainianPromptResetIntent()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("\u0432\u0456\u0434\u043d\u043e\u0432 \u043f\u0440\u043e\u043c\u043f\u0442 \u0434\u0435\u0444\u043e\u043b\u0442", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.PromptResetDefault, intent.Type);
    }

    [Fact]
    public void TryResolve_ShouldParseUkrainianSyncStatusIntent()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("\u043f\u043e\u043a\u0430\u0436\u0438 \u0441\u0442\u0430\u043d \u0441\u0438\u043d\u0445 \u0447\u0435\u0440\u0433\u0430", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.SyncStatus, intent.Type);
    }

    [Fact]
    public void TryResolve_ShouldParseUkrainianSyncRunIntentWithDefaultTake()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("\u0437\u0430\u043f\u0443\u0441\u0442\u0438 \u0441\u0438\u043d\u0445 \u0437\u0430\u0440\u0430\u0437", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.SyncRun, intent.Type);
        Assert.Equal(ConversationCommandDefaults.SyncRunTake, intent.Number);
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
    public void TryResolve_ShouldParseNaturalSyncFailedIntent()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("show failed sync jobs", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.SyncFailed, intent.Type);
        Assert.Equal(ConversationCommandDefaults.SyncFailedPreviewTake, intent.Number);
    }

    [Fact]
    public void TryResolve_ShouldParseNaturalSyncRetryFailedIntentWithNumber()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("retry failed sync 12", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.SyncRetryFailed, intent.Type);
        Assert.Equal(12, intent.Number);
    }

    [Fact]
    public void TryResolve_ShouldParseSlashSyncRetryFailedWithNumber()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("/sync retry failed 8", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.SyncRetryFailed, intent.Type);
        Assert.Equal(8, intent.Number);
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
    public void TryResolve_ShouldParseSlashPromptApplyWithInvalidIdAsNull()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("/prompt apply xyz", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.PromptApply, intent.Type);
        Assert.Null(intent.Number);
    }

    [Fact]
    public void TryResolve_ShouldParseSlashPromptRejectWithoutIdAsNull()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("/prompt reject", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.PromptReject, intent.Type);
        Assert.Null(intent.Number);
    }

    [Fact]
    public void TryResolve_ShouldParseSlashPromptProposeWithoutSeparator()
    {
        var sut = new ConversationIntentRouter();

        var handled = sut.TryResolve("/prompt propose only reason", out var intent);

        Assert.True(handled);
        Assert.Equal(ConversationCommandIntentType.PromptPropose, intent.Type);
        Assert.Equal("only reason", intent.Argument);
        Assert.Equal(string.Empty, intent.Argument2);
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
