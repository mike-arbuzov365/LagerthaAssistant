namespace LagerthaAssistant.Application.Tests.Constants;

using LagerthaAssistant.Application.Constants;
using Xunit;

public sealed class ConversationCommandCatalogTests
{
    [Fact]
    public void SlashCommands_ShouldUseUniqueCommandKeys()
    {
        var duplicateCommands = ConversationCommandCatalog.SlashCommands
            .GroupBy(item => item.Command, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.Empty(duplicateCommands);
    }

    [Fact]
    public void SlashCommands_ShouldHaveNonEmptyDescriptions()
    {
        Assert.All(
            ConversationCommandCatalog.SlashCommands,
            item => Assert.False(string.IsNullOrWhiteSpace(item.Description)));
    }

    [Fact]
    public void SlashCommands_ShouldContainExpectedCanonicalCommands()
    {
        var commands = ConversationCommandCatalog.SlashCommands
            .Select(item => item.Command)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var expectedCommands = new[]
        {
            ConversationSlashCommands.Help,
            ConversationSlashCommands.History,
            ConversationSlashCommands.Memory,
            ConversationSlashCommands.Prompt,
            ConversationSlashCommands.PromptDefault,
            ConversationSlashCommands.PromptHistory,
            ConversationSlashCommands.PromptProposals,
            $"{ConversationSlashCommands.PromptSet} <text>",
            $"{ConversationSlashCommands.PromptPropose} <reason> || <text>",
            $"{ConversationSlashCommands.PromptImprove} <goal>",
            $"{ConversationSlashCommands.PromptApply} <id>",
            $"{ConversationSlashCommands.PromptReject} <id>",
            ConversationSlashCommands.Sync,
            ConversationSlashCommands.SyncStatus,
            ConversationSlashCommands.SyncRun,
            $"{ConversationSlashCommands.SyncRun} <n>",
            ConversationSlashCommands.Reset
        };

        foreach (var command in expectedCommands)
        {
            Assert.True(commands.Contains(command), $"Expected catalog command '{command}' was not found.");
        }
    }
}