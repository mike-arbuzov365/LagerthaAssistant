namespace SharedBotKernel.Domain.Entities;

using SharedBotKernel.Domain.Base;
using SharedBotKernel.Domain.Constants;

public sealed class UserMemoryEntry : AuditableEntity
{
    public string Channel { get; set; } = ConversationScopeDefaults.Channel;

    public string UserId { get; set; } = ConversationScopeDefaults.UserId;

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset LastSeenAtUtc { get; set; }
}
