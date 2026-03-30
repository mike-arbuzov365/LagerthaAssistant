namespace SharedBotKernel.Tests.Domain.Entities;

using SharedBotKernel.Domain.Constants;
using SharedBotKernel.Domain.Entities;
using Xunit;

public sealed class UserMemoryEntryTests
{
    [Fact]
    public void NewEntry_IsActiveByDefault()
    {
        var entry = new UserMemoryEntry();
        Assert.True(entry.IsActive);
    }

    [Fact]
    public void NewEntry_HasDefaultChannelAndUserId()
    {
        var entry = new UserMemoryEntry();
        Assert.Equal(ConversationScopeDefaults.Channel, entry.Channel);
        Assert.Equal(ConversationScopeDefaults.UserId, entry.UserId);
    }

    [Fact]
    public void Entry_StoresKeyAndValue()
    {
        var entry = new UserMemoryEntry
        {
            Key = "preferred_language",
            Value = "uk"
        };
        Assert.Equal("preferred_language", entry.Key);
        Assert.Equal("uk", entry.Value);
    }

    [Fact]
    public void Entry_StoresConfidence()
    {
        var entry = new UserMemoryEntry { Confidence = 0.9 };
        Assert.Equal(0.9, entry.Confidence);
    }

    [Fact]
    public void Entry_CanBeDeactivated()
    {
        var entry = new UserMemoryEntry { IsActive = true };
        entry.IsActive = false;
        Assert.False(entry.IsActive);
    }

    [Fact]
    public void Entry_TracksLastSeenAt()
    {
        var now = DateTimeOffset.UtcNow;
        var entry = new UserMemoryEntry { LastSeenAtUtc = now };
        Assert.Equal(now, entry.LastSeenAtUtc);
    }
}
