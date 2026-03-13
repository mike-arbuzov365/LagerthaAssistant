namespace LagerthaAssistant.Api.Contracts;

public sealed record ConversationCreatePromptProposalRequest(
    string Prompt,
    string Reason,
    double? Confidence = null,
    string? Source = null);

