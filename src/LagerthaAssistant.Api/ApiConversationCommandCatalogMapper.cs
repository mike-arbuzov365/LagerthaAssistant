namespace LagerthaAssistant.Api;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Models.Agents;

internal static class ApiConversationCommandCatalogMapper
{
    public static IReadOnlyList<ConversationCommandItemResponse> MapFlatItems(
        IReadOnlyList<ConversationCommandCatalogItem> items)
    {
        return items
            .Select(item => new ConversationCommandItemResponse(item.Category, item.Command, item.Description))
            .ToList();
    }

    public static IReadOnlyList<ConversationCommandGroupResponse> MapGroupedItems(
        IReadOnlyList<ConversationCommandCatalogGroup> groups)
    {
        return groups
            .Select(group =>
                new ConversationCommandGroupResponse(
                    group.Category,
                    group.Commands
                        .Select(item => new ConversationCommandItemResponse(item.Category, item.Command, item.Description))
                        .ToList()))
            .ToList();
    }
}
