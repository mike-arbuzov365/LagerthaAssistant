namespace LagerthaAssistant.Api.Contracts;

public sealed record ConversationCommandGroupResponse(
    string Category,
    IReadOnlyList<ConversationCommandItemResponse> Commands);
