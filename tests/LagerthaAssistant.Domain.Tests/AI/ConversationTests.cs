namespace LagerthaAssistant.Domain.Tests.AI;

using LagerthaAssistant.Domain.AI;
using LagerthaAssistant.Domain.Abstractions;
using LagerthaAssistant.Domain.Constants;
using LagerthaAssistant.Domain.Exceptions;
using Xunit;

public sealed class ConversationTests
{
    [Fact]
    public void TrimHistory_Should_KeepSystemAndTail()
    {
        var clock = new FakeClock();
        var conversation = new Conversation("system", clock);

        conversation.AddUserMessage("u1", clock);
        conversation.AddAssistantMessage("a1", clock);
        conversation.AddUserMessage("u2", clock);
        conversation.AddAssistantMessage("a2", clock);

        conversation.TrimHistory(ConversationRules.MinMessagesToKeep + 1);

        var messages = conversation.Messages.ToArray();

        Assert.Equal(3, messages.Length);
        Assert.Equal(MessageRole.System, messages[0].Role);
        Assert.Equal("system", messages[0].Content);
        Assert.Equal("u2", messages[1].Content);
        Assert.Equal("a2", messages[2].Content);
    }

    [Fact]
    public void TrimHistory_Should_Throw_WhenLimitTooSmall()
    {
        var clock = new FakeClock();
        var conversation = new Conversation("system", clock);

        Assert.Throws<DomainValidationException>(() => conversation.TrimHistory(1));
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 3, 6, 12, 0, 0, TimeSpan.Zero);
    }
}

