namespace LagerthaAssistant.Application.Models.Agents;

public sealed record ConversationAgentContext(
    string Input,
    IReadOnlyList<string> BatchItems,
    ConversationScope Scope)
{
    public ConversationAgentContext(string input, IReadOnlyList<string> batchItems)
        : this(input, batchItems, ConversationScope.Default)
    {
    }

    public bool IsSlashCommand => Input.StartsWith("/", StringComparison.Ordinal);

    public bool IsBatch => BatchItems.Count > 1;
}
