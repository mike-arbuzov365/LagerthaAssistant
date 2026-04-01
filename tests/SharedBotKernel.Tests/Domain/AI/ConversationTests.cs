namespace SharedBotKernel.Tests.Domain.AI;

using SharedBotKernel.Domain.Abstractions;
using SharedBotKernel.Domain.AI;
using SharedBotKernel.Domain.Constants;
using SharedBotKernel.Domain.Exceptions;
using Xunit;

public sealed class ConversationTests
{
    [Fact]
    public void Constructor_ShouldAddSystemMessage_WhenConversationIsCreated()
    {
        var now = DateTimeOffset.Parse("2026-04-01T12:00:00+00:00");
        var clock = new FakeClock(now);

        var conversation = new Conversation("  You are a helper.  ", clock);

        var message = Assert.Single(conversation.Messages);
        Assert.Equal(MessageRole.System, message.Role);
        Assert.Equal("You are a helper.", message.Content);
        Assert.Equal(now, message.CreatedAtUtc);
    }

    [Fact]
    public void AddHistoricalMessage_ShouldIgnoreMessage_WhenRoleIsSystem()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var conversation = new Conversation("System", clock);

        conversation.AddHistoricalMessage(MessageRole.System, "Should be ignored", DateTimeOffset.UtcNow);

        Assert.Single(conversation.Messages);
    }

    [Fact]
    public void TrimHistory_ShouldKeepSystemAndMostRecentMessages_WhenCountExceedsLimit()
    {
        var clock = new FakeClock(DateTimeOffset.Parse("2026-04-01T12:00:00+00:00"));
        var conversation = new Conversation("System", clock);
        conversation.AddUserMessage("u1", clock);
        conversation.AddAssistantMessage("a1", clock);
        conversation.AddUserMessage("u2", clock);
        conversation.AddAssistantMessage("a2", clock);

        conversation.TrimHistory(3);

        var messages = conversation.Messages.ToList();
        Assert.Equal(3, messages.Count);
        Assert.Equal(MessageRole.System, messages[0].Role);
        Assert.Equal("u2", messages[1].Content);
        Assert.Equal("a2", messages[2].Content);
    }

    [Fact]
    public void TrimHistory_ShouldThrowDomainValidationException_WhenMaxMessagesTooSmall()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var conversation = new Conversation("System", clock);

        var ex = Assert.Throws<DomainValidationException>(() => conversation.TrimHistory(1));
        Assert.Contains(ConversationRules.MinMessagesToKeep.ToString(), ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConversationMessageCreate_ShouldTrimContent_WhenWhitespaceProvided()
    {
        var createdAt = DateTimeOffset.Parse("2026-04-01T12:00:00+00:00");

        var message = ConversationMessage.Create(MessageRole.User, "  hello  ", createdAt);

        Assert.Equal("hello", message.Content);
        Assert.Equal(createdAt, message.CreatedAtUtc);
    }

    [Fact]
    public void ConversationMessageCreate_ShouldThrowDomainValidationException_WhenContentEmpty()
    {
        Assert.Throws<DomainValidationException>(() => ConversationMessage.Create(
            MessageRole.User,
            "    ",
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ConversationMessageCreate_ShouldThrowDomainValidationException_WhenContentTooLong()
    {
        var tooLong = new string('x', ConversationRules.MaxContentLength + 1);

        Assert.Throws<DomainValidationException>(() => ConversationMessage.Create(
            MessageRole.User,
            tooLong,
            DateTimeOffset.UtcNow));
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
    }
}
