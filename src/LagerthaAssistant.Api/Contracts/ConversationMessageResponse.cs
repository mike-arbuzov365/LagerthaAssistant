namespace LagerthaAssistant.Api.Contracts;

public sealed record ConversationMessageResponse(
    string Agent,
    string Intent,
    bool IsBatch,
    IReadOnlyList<ConversationMessageItemResponse> Items,
    string? Message);

public sealed record ConversationMessageItemResponse(
    string Input,
    bool FoundInDeck,
    string? AssistantReply,
    string? Model,
    string? SaveStatus,
    string? TargetDeckFileName,
    string? TargetDeckPath,
    string? ExistingEntriesPreview,
    string? Warning);
