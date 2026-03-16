namespace LagerthaAssistant.UI.Constants;

using LagerthaAssistant.Application.Constants;

public static class ConsoleCommands
{
    public const string Help = ConversationSlashCommands.Help;
    public const string Batch = "/batch";

    public const string Save = "/save";
    public const string SaveMode = "/save mode";

    public const string Storage = "/storage";
    public const string StorageMode = "/storage mode";

    public const string GraphStatus = "/graph status";
    public const string GraphLogin = "/graph login";
    public const string GraphLogout = "/graph logout";

    public const string Sync = ConversationSlashCommands.Sync;
    public const string SyncStatus = ConversationSlashCommands.SyncStatus;
    public const string SyncRun = ConversationSlashCommands.SyncRun;

    public const string Exit = "/exit";
    public const string Reset = ConversationSlashCommands.Reset;

    public const string IndexClear = ConversationSlashCommands.IndexClear;
    public const string IndexRebuild = ConversationSlashCommands.IndexRebuild;

    public const string History = ConversationSlashCommands.History;
    public const string Memory = ConversationSlashCommands.Memory;

    public const string Prompt = ConversationSlashCommands.Prompt;
    public const string PromptDefault = ConversationSlashCommands.PromptDefault;
    public const string PromptHistory = ConversationSlashCommands.PromptHistory;
    public const string PromptProposals = ConversationSlashCommands.PromptProposals;
    public const string PromptSet = ConversationSlashCommands.PromptSet;
    public const string PromptPropose = ConversationSlashCommands.PromptPropose;
    public const string PromptImprove = ConversationSlashCommands.PromptImprove;
    public const string PromptApply = ConversationSlashCommands.PromptApply;
    public const string PromptReject = ConversationSlashCommands.PromptReject;

    public const int HistoryPreviewTake = 20;
    public const int MemoryPreviewTake = 20;
    public const int PromptHistoryTake = 10;
    public const int PromptProposalsTake = 20;
}
