namespace LagerthaAssistant.Application.Constants;

public static class ConversationSlashCommands
{
    public const string Help = "/help";
    public const string History = "/history";
    public const string Memory = "/memory";

    public const string Prompt = "/prompt";
    public const string PromptDefault = "/prompt default";
    public const string PromptHistory = "/prompt history";
    public const string PromptSet = "/prompt set";

    public const string Sync = "/sync";
    public const string SyncStatus = "/sync status";
    public const string SyncFailed = "/sync failed";
    public const string SyncRun = "/sync run";
    public const string SyncRetryFailed = "/sync retry failed";

    public const string Reset = "/reset";

    public const string Legacy = "/legacy";

    public const string Index = "/index";
    public const string IndexClear = "/index clear";
    public const string IndexRebuild = "/index rebuild";
}
