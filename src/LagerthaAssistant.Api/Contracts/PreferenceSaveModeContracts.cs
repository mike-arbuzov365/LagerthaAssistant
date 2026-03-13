namespace LagerthaAssistant.Api.Contracts;

public sealed record PreferenceSaveModeResponse(
    string Mode,
    IReadOnlyList<string> AvailableModes);

public sealed record PreferenceSetSaveModeRequest(
    string Mode,
    string? Channel = null,
    string? UserId = null,
    string? ConversationId = null);

public sealed record PreferenceSessionResponse(
    string SaveMode,
    IReadOnlyList<string> AvailableSaveModes,
    string StorageMode,
    IReadOnlyList<string> AvailableStorageModes);

public sealed record PreferenceSetSessionRequest(
    string? SaveMode = null,
    string? StorageMode = null,
    string? Channel = null,
    string? UserId = null,
    string? ConversationId = null);
