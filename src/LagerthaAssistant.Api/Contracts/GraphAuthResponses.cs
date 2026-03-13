namespace LagerthaAssistant.Api.Contracts;

public sealed record GraphAuthStatusResponse(
    bool IsConfigured,
    bool IsAuthenticated,
    string Message,
    DateTimeOffset? AccessTokenExpiresAtUtc);

public sealed record GraphLoginResponse(
    bool Succeeded,
    string Message,
    GraphAuthStatusResponse Status);
