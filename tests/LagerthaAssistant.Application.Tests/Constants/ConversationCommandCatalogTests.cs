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
            { ConversationSlashCommands.SyncFailed, ConversationCommandCategories.SyncQueue },
            { ConversationSlashCommands.SyncRun, ConversationCommandCategories.SyncQueue },
            { $"{ConversationSlashCommands.SyncRun} <n>", ConversationCommandCategories.SyncQueue },
            { ConversationSlashCommands.SyncRetryFailed, ConversationCommandCategories.SyncQueue },
            { $"{ConversationSlashCommands.SyncRetryFailed} <n>", ConversationCommandCategories.SyncQueue },
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
    public void SlashCommands_ShouldUseOnlyKnownCategories()
    {
        var knownCategories = ConversationCommandCategories.Ordered
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.All(
            ConversationCommandCatalog.SlashCommands,
            item => Assert.True(
                knownCategories.Contains(item.Category),
                $"Unknown command category '{item.Category}' for command '{item.Command}'."));
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
            ConversationSlashCommands.SyncFailed,
            ConversationSlashCommands.SyncRun,
            $"{ConversationSlashCommands.SyncRun} <n>",
            ConversationSlashCommands.SyncRetryFailed,
            $"{ConversationSlashCommands.SyncRetryFailed} <n>",
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

    [Fact]
    public void SlashCommandGroups_ShouldPreserveCategoryOrder_AndContainAllCommands()
    {
        var groups = ConversationCommandCatalog.SlashCommandGroups;

        var expectedCategoryOrder = ConversationCommandCategories.Ordered;
        Assert.Equal(expectedCategoryOrder, groups.Select(group => group.Category));

        var flattenedGroupCommands = groups
            .SelectMany(group => group.Commands)
            .Select(item => item.Command)
            .ToList();

        var sourceCommands = ConversationCommandCatalog.SlashCommands
            .Select(item => item.Command)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(sourceCommands.Count, flattenedGroupCommands.Count);
        Assert.Equal(sourceCommands.Count, flattenedGroupCommands.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var command in sourceCommands)
        {
            Assert.True(
                flattenedGroupCommands.Contains(command, StringComparer.OrdinalIgnoreCase),
                $"Grouped catalog is missing command '{command}'.");
        }
    }

    [Fact]
    public void SlashCommandGroups_ShouldContainOnlyCommandsFromOwnCategory()
    {
        Assert.All(
            ConversationCommandCatalog.SlashCommandGroups,
            group => Assert.All(group.Commands, item => Assert.Equal(group.Category, item.Category)));
    }
}
