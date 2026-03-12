namespace LagerthaAssistant.Infrastructure.Options;

public sealed class GraphOptions
{
    public string TenantId { get; init; } = "common";

    public string ClientId { get; init; } = string.Empty;

    public IReadOnlyList<string> Scopes { get; init; } = ["User.Read", "Files.ReadWrite", "offline_access"];

    public string RootPath { get; init; } = "/Apps/Flashcards Deluxe";

    public string TokenCachePath { get; init; } = "%LOCALAPPDATA%\\LagerthaAssistant\\graph-token.json";
}
