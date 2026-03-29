namespace BaguetteDesign.Domain.Models;

public sealed record BriefValidationResult(
    bool IsComplete,
    double CompletenessScore,
    IReadOnlyList<string> MissingRequiredFields);
