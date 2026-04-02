namespace LagerthaAssistant.Api.Contracts;

public sealed record SessionScopeResponse(
    string Channel,
    string UserId,
    string ConversationId);

public sealed record SessionBootstrapResponse(
    SessionScopeResponse Scope,
    PreferenceLocaleResponse Locale,
    PreferenceSessionResponse Preferences,
    MiniAppPolicyResponse Policy,
    GraphAuthStatusResponse Graph,
    MiniAppSettingsBootstrapResponse Settings,
    IReadOnlyList<ConversationCommandGroupResponse> CommandGroups,
    IReadOnlyList<VocabularyPartOfSpeechOptionResponse> PartOfSpeechOptions,
    IReadOnlyList<VocabularyDeckInfoResponse>? WritableDecks);
