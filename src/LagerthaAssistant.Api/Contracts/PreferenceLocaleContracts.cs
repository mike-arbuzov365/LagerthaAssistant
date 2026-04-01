namespace LagerthaAssistant.Api.Contracts;

public sealed record PreferenceLocaleResponse(
    string Locale,
    IReadOnlyList<string> AvailableLocales);

public sealed record PreferenceSetLocaleRequest(
    string Locale,
    bool SelectedManually = true,
    string? Channel = null,
    string? UserId = null,
    string? ConversationId = null);
