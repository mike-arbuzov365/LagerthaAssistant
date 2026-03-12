namespace LagerthaAssistant.Application.Constants;

using LagerthaAssistant.Application.Models.Agents;

public static class ConversationCommandCatalog
{
    public static IReadOnlyList<ConversationCommandCatalogItem> SlashCommands { get; } =
    [
        new(ConversationSlashCommands.Help, "Show command help."),
        new(ConversationSlashCommands.History, "Show recent conversation history."),
        new(ConversationSlashCommands.Memory, "Show active memory facts."),
        new(ConversationSlashCommands.Prompt, "Show current system prompt."),
        new(ConversationSlashCommands.PromptDefault, "Reset system prompt to default."),
        new(ConversationSlashCommands.PromptHistory, "Show saved prompt versions."),
        new(ConversationSlashCommands.PromptProposals, "Show prompt proposals."),
        new($"{ConversationSlashCommands.PromptSet} <text>", "Set system prompt from text."),
        new($"{ConversationSlashCommands.PromptPropose} <reason> || <text>", "Create manual prompt proposal."),
        new($"{ConversationSlashCommands.PromptImprove} <goal>", "Generate AI prompt proposal for a goal."),
        new($"{ConversationSlashCommands.PromptApply} <id>", "Apply a prompt proposal."),
        new($"{ConversationSlashCommands.PromptReject} <id>", "Reject a prompt proposal."),
        new(ConversationSlashCommands.Sync, "Show pending sync jobs."),
        new(ConversationSlashCommands.SyncStatus, "Alias for sync status."),
        new(ConversationSlashCommands.SyncRun, "Run pending sync jobs with default batch size."),
        new($"{ConversationSlashCommands.SyncRun} <n>", "Run up to <n> pending sync jobs."),
        new(ConversationSlashCommands.Reset, "Reset conversation context.")
    ];
}
