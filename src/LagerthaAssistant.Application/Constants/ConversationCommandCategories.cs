namespace LagerthaAssistant.Application.Constants;

public static class ConversationCommandCategories
{
    public const string General = "General";
    public const string Conversation = "Conversation";
    public const string SystemPrompt = "System prompt";
    public const string VocabularyIndex = "Vocabulary index";
    public const string SyncQueue = "Sync queue";
    public const string Session = "Session";

    public static IReadOnlyList<string> Ordered { get; } =
    [
        General,
        Conversation,
        SystemPrompt,
        VocabularyIndex,
        SyncQueue,
        Session
    ];
}
