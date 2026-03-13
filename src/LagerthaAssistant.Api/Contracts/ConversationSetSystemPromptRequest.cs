namespace LagerthaAssistant.Api.Contracts;

public sealed record ConversationSetSystemPromptRequest(
    string Prompt,
    string? Source = null);

