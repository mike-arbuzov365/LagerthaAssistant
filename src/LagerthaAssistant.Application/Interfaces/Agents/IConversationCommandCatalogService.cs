namespace LagerthaAssistant.Application.Interfaces.Agents;

using LagerthaAssistant.Application.Models.Agents;

public interface IConversationCommandCatalogService
{
    IReadOnlyList<ConversationCommandCatalogItem> GetCommands();

    IReadOnlyList<ConversationCommandCatalogGroup> GetGroups();
}
