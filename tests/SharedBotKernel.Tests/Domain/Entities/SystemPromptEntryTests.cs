namespace SharedBotKernel.Tests.Domain.Entities;

using SharedBotKernel.Domain.Entities;
using Xunit;

public sealed class SystemPromptEntryTests
{
    [Fact]
    public void Entry_StoresPromptText()
    {
        var entry = new SystemPromptEntry { PromptText = "You are a helpful assistant." };
        Assert.Equal("You are a helpful assistant.", entry.PromptText);
    }

    [Fact]
    public void Entry_StoresVersion()
    {
        var entry = new SystemPromptEntry { Version = 3 };
        Assert.Equal(3, entry.Version);
    }

    [Fact]
    public void Entry_IsNotActiveByDefault()
    {
        var entry = new SystemPromptEntry();
        Assert.False(entry.IsActive);
    }

    [Fact]
    public void Entry_CanBeActivated()
    {
        var entry = new SystemPromptEntry { IsActive = true };
        Assert.True(entry.IsActive);
    }

    [Fact]
    public void Entry_StoresSource()
    {
        var entry = new SystemPromptEntry { Source = "appsettings" };
        Assert.Equal("appsettings", entry.Source);
    }

    [Fact]
    public void TwoEntries_WithDifferentVersions_AreDistinct()
    {
        var v1 = new SystemPromptEntry { Version = 1, PromptText = "v1 text", IsActive = false };
        var v2 = new SystemPromptEntry { Version = 2, PromptText = "v2 text", IsActive = true };

        Assert.NotEqual(v1.Version, v2.Version);
        Assert.NotEqual(v1.PromptText, v2.PromptText);
        Assert.NotEqual(v1.IsActive, v2.IsActive);
    }
}
