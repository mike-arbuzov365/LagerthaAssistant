namespace LagerthaAssistant.Application.Models.Agents;

public sealed record ConversationAgentResult(
    string AgentName,
    string Intent,
    bool IsBatch,
    IReadOnlyList<ConversationAgentItemResult> Items,
    string? Message = null)
{
    public static ConversationAgentResult Empty(string agentName, string intent, string? message = null)
        => new(agentName, intent, false, [], message);
}
