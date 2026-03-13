namespace LagerthaAssistant.Api.Contracts;

public sealed record VocabularyStorageModeResponse(
    string Mode,
    IReadOnlyList<string> AvailableModes);

public sealed record VocabularySetStorageModeRequest(
    string Mode,
    string? Channel = null,
    string? UserId = null,
    string? ConversationId = null);
