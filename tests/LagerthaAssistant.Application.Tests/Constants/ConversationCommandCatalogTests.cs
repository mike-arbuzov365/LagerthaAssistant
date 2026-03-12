namespace LagerthaAssistant.Application.Tests.Constants;

using LagerthaAssistant.Application.Constants;
using Xunit;

public sealed class ConversationCommandCatalogTests
{
    public static TheoryData<string, string> ExpectedCommandCategories =>
        new()
        {
            { ConversationSlashCommands.Help, ConversationCommandCategories.General },
            { ConversationSlashCommands.History, ConversationCommandCategories.Conversation },
            { ConversationSlashCommands.Memory, ConversationCommandCategories.Conversation },
            { ConversationSlashCommands.Prompt, ConversationCommandCategories.SystemPrompt },
            { ConversationSlashCommands.PromptDefault, ConversationCommandCategories.SystemPrompt },
            { ConversationSlashCommands.PromptHistory, ConversationCommandCategories.SystemPrompt },
            { $"{ConversationSlashCommands.PromptSet} <text>", ConversationCommandCategories.SystemPrompt },
            { ConversationSlashCommands.PromptProposals, ConversationCommandCategories.PromptProposals },
            { $"{ConversationSlashCommands.PromptPropose} <reason> || <text>", ConversationCommandCategories.PromptProposals },
            { $"{ConversationSlashCommands.PromptImprove} <goal>", ConversationCommandCategories.PromptProposals },
            { $"{ConversationSlashCommands.PromptApply} <id>", ConversationCommandCategories.PromptProposals },
            { $"{ConversationSlashCommands.PromptReject} <id>", ConversationCommandCategories.PromptProposals },
            { ConversationSlashCommands.Sync, ConversationCommandCategories.SyncQueue },
            { ConversationSlashCommands.SyncStatus, ConversationCommandCategories.SyncQueue },
            { ConversationSlashCommands.SyncRun, ConversationCommandCategories.SyncQueue },
            { $"{ConversationSlashCommands.SyncRun} <n>", ConversationCommandCategories.SyncQueue },
            { ConversationSlashCommands.Reset, ConversationCommandCategories.Session }
        };

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
    public void SlashCommands_ShouldHaveNonEmptyDescriptionsAndCategories()
    {
        Assert.All(
            ConversationCommandCatalog.SlashCommands,
            item =>
            {
                Assert.False(string.IsNullOrWhiteSpace(item.Description));
                Assert.False(string.IsNullOrWhiteSpace(item.Category));
            });
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

    [Theory]
    [MemberData(nameof(ExpectedCommandCategories))]
    public void SlashCommands_ShouldAssignExpectedCategories(string command, string expectedCategory)
    {
        var item = Assert.Single(
            ConversationCommandCatalog.SlashCommands,
            candidate => candidate.Command.Equals(command, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(expectedCategory, item.Category);
    }
}