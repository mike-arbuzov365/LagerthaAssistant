namespace LagerthaAssistant.Application.Models.Agents;

public enum ConversationCommandIntentType
{
    Help,
    History,
    Memory,
    PromptShow,
    PromptResetDefault,
    PromptHistory,
    PromptSet,
    SyncStatus,
    SyncFailed,
    SyncRun,
    SyncRetryFailed,
    ResetConversation,
    IndexHelp,
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
