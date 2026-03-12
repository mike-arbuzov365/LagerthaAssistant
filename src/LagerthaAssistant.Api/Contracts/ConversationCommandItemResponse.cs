namespace LagerthaAssistant.Api.Contracts;

public sealed record ConversationCommandItemResponse(
    string Category,
    string Command,
    string Description);
