namespace LagerthaAssistant.Api.Contracts;

public sealed record SessionScopeResponse(
    string Channel,
    string UserId,
    string ConversationId);

public sealed record SessionBootstrapResponse(
    SessionScopeResponse Scope,
    PreferenceSessionResponse Preferences,
    GraphAuthStatusResponse Graph,
    IReadOnlyList<ConversationCommandGroupResponse> CommandGroups,
    IReadOnlyList<VocabularyPartOfSpeechOptionResponse> PartOfSpeechOptions,
    IReadOnlyList<VocabularyDeckInfoResponse>? WritableDecks);
