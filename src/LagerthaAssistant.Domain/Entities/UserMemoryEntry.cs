namespace LagerthaAssistant.Domain.Entities;

using LagerthaAssistant.Domain.Common.Base;

public sealed class UserMemoryEntry : AuditableEntity
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset LastSeenAtUtc { get; set; }
}

