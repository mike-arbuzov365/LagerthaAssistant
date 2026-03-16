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
    PromptSet,
    PromptPropose,
    PromptImprove,
    PromptApply,
    PromptReject,
    SyncStatus,
    SyncFailed,
    SyncRun,
    SyncRetryFailed,
    ResetConversation,
    IndexClear,
    IndexRebuild,
    Unsupported
}

public sealed record ConversationCommandIntent(
    ConversationCommandIntentType Type,
    int? Number = null,
    string? Raw = null,
    string? Argument = null,
    string? Argument2 = null);
