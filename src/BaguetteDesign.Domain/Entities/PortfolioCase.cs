namespace BaguetteDesign.Domain.Entities;

public sealed class PortfolioCase : AuditableEntity
{
    public string NotionPageId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Tags { get; set; }
    public string? CoverImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
}
