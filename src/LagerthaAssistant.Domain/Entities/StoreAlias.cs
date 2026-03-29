namespace LagerthaAssistant.Domain.Entities;


public sealed class StoreAlias : AuditableEntity
{
    public string DetectedPattern { get; set; } = string.Empty;

    public string ResolvedStoreName { get; set; } = string.Empty;
}
