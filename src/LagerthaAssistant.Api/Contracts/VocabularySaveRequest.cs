namespace LagerthaAssistant.Api.Contracts;

public sealed record VocabularySaveRequest(
    string RequestedWord,
    string AssistantReply,
    string? ForcedDeckFileName = null,
    string? OverridePartOfSpeech = null);

