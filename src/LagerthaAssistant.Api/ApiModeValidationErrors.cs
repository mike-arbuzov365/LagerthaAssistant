namespace LagerthaAssistant.Api;

internal static class ApiModeValidationErrors
{
    public static string BuildUnsupported(
        string modeLabel,
        string? requestedValue,
        IReadOnlyList<string> supportedModes)
    {
        var label = string.IsNullOrWhiteSpace(modeLabel) ? "mode" : modeLabel.Trim();
        var requested = (requestedValue ?? string.Empty).Trim();

        if (supportedModes.Count == 0)
        {
            return $"Unsupported {label} '{requested}'.";
        }

        return $"Unsupported {label} '{requested}'. Use one of: {string.Join(", ", supportedModes)}.";
    }
}
