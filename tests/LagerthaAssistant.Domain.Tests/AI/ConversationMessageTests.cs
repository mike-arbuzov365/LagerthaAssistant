namespace LagerthaAssistant.Domain.Tests.AI;

using LagerthaAssistant.Domain.AI;
using LagerthaAssistant.Domain.Constants;
using LagerthaAssistant.Domain.Exceptions;
using Xunit;

public sealed class ConversationMessageTests
{
    [Fact]
    public void Create_Should_TrimContent()
    {
        var message = ConversationMessage.Create(MessageRole.User, "  hello  ", DateTimeOffset.UtcNow);

        Assert.Equal("hello", message.Content);
    }

    [Fact]
    public void Create_Should_Throw_WhenContentEmpty()
    {
        Assert.Throws<DomainValidationException>(() =>
            ConversationMessage.Create(MessageRole.User, "   ", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Create_Should_Throw_WhenContentTooLong()
    {
        var content = new string('x', ConversationRules.MaxContentLength + 1);

        Assert.Throws<DomainValidationException>(() =>
            ConversationMessage.Create(MessageRole.User, content, DateTimeOffset.UtcNow));
    }
}

