namespace LagerthaAssistant.Application.Models.Agents;

public sealed record ConversationAgentContext(
    string Input,
    IReadOnlyList<string> BatchItems,
    ConversationScope Scope,
    ConversationCommandIntent? ResolvedIntent = null)
{
    public ConversationAgentContext(string input, IReadOnlyList<string> batchItems)
        : this(input, batchItems, ConversationScope.Default, null)
    {
    }

    public bool IsSlashCommand => Input.StartsWith("/", StringComparison.Ordinal);

    public bool IsBatch => BatchItems.Count > 1;

    public bool HasResolvedCommandIntent
        => ResolvedIntent is not null && ResolvedIntent.Type != ConversationCommandIntentType.Unsupported;
}
