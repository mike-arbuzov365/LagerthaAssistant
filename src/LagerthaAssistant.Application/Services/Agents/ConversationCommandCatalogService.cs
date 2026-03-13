namespace LagerthaAssistant.Application.Services.Agents;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Models.Agents;

public sealed class ConversationCommandCatalogService : IConversationCommandCatalogService
{
    public IReadOnlyList<ConversationCommandCatalogItem> GetCommands()
        => ConversationCommandCatalog.SlashCommands;

    public IReadOnlyList<ConversationCommandCatalogGroup> GetGroups()
        => ConversationCommandCatalog.SlashCommandGroups;
}
