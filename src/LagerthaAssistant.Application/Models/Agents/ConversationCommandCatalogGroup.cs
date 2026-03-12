namespace LagerthaAssistant.Application.Models.Agents;

public sealed record ConversationCommandCatalogGroup(
    string Category,
    IReadOnlyList<ConversationCommandCatalogItem> Commands);