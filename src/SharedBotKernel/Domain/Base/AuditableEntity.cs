namespace SharedBotKernel.Domain.Base;

public abstract class AuditableEntity : BaseEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? TenantId { get; set; }
}
