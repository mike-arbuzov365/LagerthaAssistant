namespace LagerthaAssistant.Application.Models.Agents;

public sealed record ConversationBootstrapOptions(
    bool IncludeCommandGroups = true,
    bool IncludePartOfSpeechOptions = true,
    bool IncludeWritableDecks = false)
{
    public static ConversationBootstrapOptions Default { get; } = new();
}
