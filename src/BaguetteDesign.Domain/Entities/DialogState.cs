namespace BaguetteDesign.Domain.Entities;

using BaguetteDesign.Domain.Enums;

public sealed class DialogState : AuditableEntity
{
    public string ClientUserId { get; set; } = string.Empty;
    public DialogStatus Status { get; set; } = DialogStatus.New;
    public string? LastClientMessagePreview { get; set; }
    public DateTimeOffset? LastClientMessageAt { get; set; }
}
