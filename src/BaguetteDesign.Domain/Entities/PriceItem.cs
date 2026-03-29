namespace BaguetteDesign.Domain.Entities;

public sealed class PriceItem : AuditableEntity
{
    public string NotionPageId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? PriceAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Country { get; set; }
    public bool IsActive { get; set; } = true;
}
