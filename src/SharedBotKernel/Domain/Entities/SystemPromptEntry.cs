namespace SharedBotKernel.Domain.Entities;

using SharedBotKernel.Domain.Base;

public sealed class SystemPromptEntry : AuditableEntity
{
    public string PromptText { get; set; } = string.Empty;

    public int Version { get; set; }

    public bool IsActive { get; set; }

    public string Source { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
