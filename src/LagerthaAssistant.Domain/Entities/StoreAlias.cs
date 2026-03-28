namespace LagerthaAssistant.Domain.Entities;

using LagerthaAssistant.Domain.Common.Base;

public sealed class StoreAlias : AuditableEntity
{
    public string DetectedPattern { get; set; } = string.Empty;

    public string ResolvedStoreName { get; set; } = string.Empty;
}
