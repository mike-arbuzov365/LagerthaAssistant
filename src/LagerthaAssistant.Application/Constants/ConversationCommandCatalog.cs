namespace LagerthaAssistant.Application.Constants;

using LagerthaAssistant.Application.Models.Agents;

public static class ConversationCommandCatalog
{
    public static IReadOnlyList<ConversationCommandCatalogItem> SlashCommands { get; } =
    [
        new(ConversationSlashCommands.Help, "Show command help.", ConversationCommandCategories.General),
        new(ConversationSlashCommands.History, "Show recent conversation history.", ConversationCommandCategories.Conversation),
        new(ConversationSlashCommands.Memory, "Show active memory facts.", ConversationCommandCategories.Conversation),
        new(ConversationSlashCommands.Prompt, "Show current system prompt.", ConversationCommandCategories.SystemPrompt),
        new(ConversationSlashCommands.PromptDefault, "Reset system prompt to default.", ConversationCommandCategories.SystemPrompt),
        new(ConversationSlashCommands.PromptHistory, "Show saved prompt versions.", ConversationCommandCategories.SystemPrompt),
        new(ConversationSlashCommands.PromptProposals, "Show prompt proposals.", ConversationCommandCategories.PromptProposals),
        new($"{ConversationSlashCommands.PromptSet} <text>", "Set system prompt from text.", ConversationCommandCategories.SystemPrompt),
        new($"{ConversationSlashCommands.PromptPropose} <reason> || <text>", "Create manual prompt proposal.", ConversationCommandCategories.PromptProposals),
        new($"{ConversationSlashCommands.PromptImprove} <goal>", "Generate AI prompt proposal for a goal.", ConversationCommandCategories.PromptProposals),
        new($"{ConversationSlashCommands.PromptApply} <id>", "Apply a prompt proposal.", ConversationCommandCategories.PromptProposals),
        new($"{ConversationSlashCommands.PromptReject} <id>", "Reject a prompt proposal.", ConversationCommandCategories.PromptProposals),
        new(ConversationSlashCommands.Sync, "Show pending sync jobs.", ConversationCommandCategories.SyncQueue),
        new(ConversationSlashCommands.SyncStatus, "Alias for sync status.", ConversationCommandCategories.SyncQueue),
        new(ConversationSlashCommands.SyncRun, "Run pending sync jobs with default batch size.", ConversationCommandCategories.SyncQueue),
        new($"{ConversationSlashCommands.SyncRun} <n>", "Run up to <n> pending sync jobs.", ConversationCommandCategories.SyncQueue),
        new(ConversationSlashCommands.Reset, "Reset conversation context.", ConversationCommandCategories.Session)
    ];

    public static IReadOnlyList<ConversationCommandCatalogGroup> SlashCommandGroups { get; } =
        ConversationCommandCategories.Ordered
            .Select(category =>
                new ConversationCommandCatalogGroup(
                    category,
                    SlashCommands
                        .Where(item => item.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                        .ToList()))
            .Where(group => group.Commands.Count > 0)
            .ToList();
}