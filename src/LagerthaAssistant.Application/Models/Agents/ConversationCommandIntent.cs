namespace LagerthaAssistant.Application.Models.Agents;

public enum ConversationCommandIntentType
{
    Help,
    History,
    Memory,
    PromptShow,
    PromptResetDefault,
    PromptHistory,
    PromptProposals,
    SyncStatus,
    SyncRun,
    ResetConversation,
    Unsupported
}

public sealed record ConversationCommandIntent(
    ConversationCommandIntentType Type,
    int? Number = null,
    string? Raw = null);
