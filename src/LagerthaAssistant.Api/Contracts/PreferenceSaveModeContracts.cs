namespace LagerthaAssistant.Api.Contracts;

public sealed record PreferenceSaveModeResponse(
    string Mode,
    IReadOnlyList<string> AvailableModes);

public sealed record PreferenceSetSaveModeRequest(
    string Mode,
    string? Channel = null,
    string? UserId = null,
    string? ConversationId = null);
