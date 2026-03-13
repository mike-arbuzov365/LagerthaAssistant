namespace LagerthaAssistant.Application.Tests.Services.Agents;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Services.Agents;
using Xunit;

public sealed class ConversationCommandCatalogServiceTests
{
    [Fact]
    public void GetCommands_ShouldReturnConfiguredCatalogItems()
    {
        var sut = new ConversationCommandCatalogService();

        var commands = sut.GetCommands();

        Assert.NotEmpty(commands);
        Assert.Contains(commands, item => item.Command == ConversationSlashCommands.Help);
        Assert.Contains(commands, item => item.Command == $"{ConversationSlashCommands.SyncRun} <n>");
        Assert.Contains(commands, item => item.Command == ConversationSlashCommands.SyncFailed);
        Assert.Contains(commands, item => item.Command == $"{ConversationSlashCommands.SyncRetryFailed} <n>");
    }

    [Fact]
    public void GetGroups_ShouldReturnCommandsGroupedByCategory()
    {
        var sut = new ConversationCommandCatalogService();

        var groups = sut.GetGroups();

        Assert.NotEmpty(groups);

        var general = Assert.Single(groups, group => group.Category == ConversationCommandCategories.General);
        Assert.Contains(general.Commands, command => command.Command == ConversationSlashCommands.Help);
    }
}
