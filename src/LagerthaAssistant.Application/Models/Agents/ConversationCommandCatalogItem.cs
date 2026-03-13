namespace LagerthaAssistant.Application.Models.Agents;

public sealed record ConversationCommandCatalogItem(
    string Command,
    string Description,
    string Category);
