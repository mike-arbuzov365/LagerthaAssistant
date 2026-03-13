namespace LagerthaAssistant.Api.Contracts;

public sealed record ConversationSystemPromptProposalResponse(
    int Id,
    string ProposedPrompt,
    string Reason,
    double Confidence,
    string Source,
    string Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReviewedAtUtc,
    int? AppliedSystemPromptEntryId);

