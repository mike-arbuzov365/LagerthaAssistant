namespace LagerthaAssistant.Application.Models.Agents;

public sealed record ConversationAgentContext(
    string Input,
    IReadOnlyList<string> BatchItems)
{
    public bool IsSlashCommand => Input.StartsWith("/", StringComparison.Ordinal);

    public bool IsBatch => BatchItems.Count > 1;
}
