namespace LagerthaAssistant.Domain.Entities;

using LagerthaAssistant.Domain.Common.Base;

public sealed class SystemPromptProposal : AuditableEntity
{
    public string ProposedPrompt { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public string Source { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? ReviewedAtUtc { get; set; }

    public int? AppliedSystemPromptEntryId { get; set; }
}
