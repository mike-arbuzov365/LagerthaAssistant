namespace BaguetteDesign.Domain.Entities;

using BaguetteDesign.Domain.Enums;

public sealed class Lead : AuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public string? ServiceType { get; set; }
    public string? Brand { get; set; }
    public string? Audience { get; set; }
    public string? Style { get; set; }
    public string? Deadline { get; set; }
    public string? Budget { get; set; }
    public string? Country { get; set; }
    public string? AiSummary { get; set; }
    public LeadStatus Status { get; set; } = LeadStatus.New;

    public static Lead FromBriefState(string userId, BaguetteDesign.Domain.Models.BriefFlowState state)
        => new()
        {
            UserId = userId,
            ServiceType = state.ServiceType,
            Brand = state.Brand,
            Audience = state.Audience,
            Style = state.Style,
            Deadline = state.Deadline,
            Budget = state.Budget,
            Country = state.Country,
            AiSummary = state.AiSummary,
            Status = LeadStatus.New
        };
}
