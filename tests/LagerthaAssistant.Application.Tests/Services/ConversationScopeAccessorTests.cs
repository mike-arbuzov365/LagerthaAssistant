namespace LagerthaAssistant.Application.Tests.Services;

using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Services;
using Xunit;

public sealed class ConversationScopeAccessorTests
{
    [Fact]
    public void Current_ShouldReturnDefault_BeforeSet()
    {
        var sut = new ConversationScopeAccessor();

        Assert.Equal(ConversationScope.Default, sut.Current);
    }

    [Fact]
    public void Current_ShouldReturnSetValue_AfterSet()
    {
        var sut = new ConversationScopeAccessor();
        var scope = new ConversationScope("telegram", "user-1", "conv-1");

        sut.Set(scope);

        Assert.Equal(scope, sut.Current);
    }

    [Fact]
    public void Set_WithNull_ShouldFallBackToDefault()
    {
        var sut = new ConversationScopeAccessor();
        sut.Set(new ConversationScope("telegram", "user-1", "conv-1"));

        sut.Set(null!);

        Assert.Equal(ConversationScope.Default, sut.Current);
    }

    [Fact]
    public async Task Current_ShouldFlowCorrectly_AcrossAsyncContinuation()
    {
        var sut = new ConversationScopeAccessor();
        var scope = new ConversationScope("telegram", "async-user", "async-conv");

        sut.Set(scope);

        // Await a continuation — value must be visible after await
        await Task.Yield();

        Assert.Equal(scope, sut.Current);
    }
}
