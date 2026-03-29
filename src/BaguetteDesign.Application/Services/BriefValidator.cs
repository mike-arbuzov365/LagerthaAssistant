namespace BaguetteDesign.Application.Services;

using BaguetteDesign.Domain.Models;

public sealed class BriefValidator
{
    private static readonly string[] AllFields = ["service_type", "brand", "audience", "style", "deadline", "budget", "country"];
    private static readonly string[] RequiredFields = ["service_type", "budget", "deadline"];

    public BriefValidationResult Validate(BriefFlowState state)
    {
        var fieldValues = new Dictionary<string, string?>
        {
            ["service_type"] = state.ServiceType,
            ["brand"]        = state.Brand,
            ["audience"]     = state.Audience,
            ["style"]        = state.Style,
            ["deadline"]     = state.Deadline,
            ["budget"]       = state.Budget,
            ["country"]      = state.Country
        };

        var filled = fieldValues.Values.Count(v => !string.IsNullOrWhiteSpace(v));
        var score = (double)filled / AllFields.Length;

        var missing = RequiredFields
            .Where(f => string.IsNullOrWhiteSpace(fieldValues[f]))
            .ToList();

        return new BriefValidationResult(
            IsComplete: missing.Count == 0,
            CompletenessScore: score,
            MissingRequiredFields: missing);
    }
}
