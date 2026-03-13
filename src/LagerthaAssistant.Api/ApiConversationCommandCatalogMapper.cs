namespace LagerthaAssistant.Api;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Constants;

internal static class ApiConversationCommandCatalogMapper
{
    public static IReadOnlyList<ConversationCommandItemResponse> BuildFlatItems()
    {
        return ConversationCommandCatalog.SlashCommands
            .Select(item => new ConversationCommandItemResponse(item.Category, item.Command, item.Description))
            .ToList();
    }

    public static IReadOnlyList<ConversationCommandGroupResponse> BuildGroupedItems()
    {
        return ConversationCommandCatalog.SlashCommandGroups
            .Select(group =>
                new ConversationCommandGroupResponse(
                    group.Category,
                    group.Commands
                        .Select(item => new ConversationCommandItemResponse(item.Category, item.Command, item.Description))
                        .ToList()))
            .ToList();
    }
}
