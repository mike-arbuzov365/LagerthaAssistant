namespace BaguetteDesign.Domain.Entities;

using BaguetteDesign.Domain.Enums;

public sealed class Project : AuditableEntity
{
    public string ClientUserId { get; set; } = string.Empty;
    public int? LeadId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? ServiceType { get; set; }
    public string? Budget { get; set; }
    public string? Deadline { get; set; }
    public string? GoogleDriveFolderUrl { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public int RevisionCount { get; set; }
    public int MaxRevisions { get; set; } = 3;

    public bool IsRevisionLimitReached => RevisionCount >= MaxRevisions;

    public static Project FromLead(Lead lead, int maxRevisions = 3) => new()
    {
        ClientUserId = lead.UserId,
        LeadId = lead.Id,
        Title = $"{lead.ServiceType ?? "Project"} — {lead.Brand ?? lead.UserId}",
        ServiceType = lead.ServiceType,
        Budget = lead.Budget,
        Deadline = lead.Deadline,
        MaxRevisions = maxRevisions
    };
}
