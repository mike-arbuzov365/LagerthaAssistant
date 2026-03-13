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

public sealed record GraphDeviceLoginChallengeResponse(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    int ExpiresInSeconds,
    int IntervalSeconds,
    DateTimeOffset ExpiresAtUtc,
    string? Message);

public sealed record GraphDeviceLoginStartResponse(
    bool Succeeded,
    string Message,
    GraphDeviceLoginChallengeResponse? Challenge);

public sealed record GraphDeviceLoginCompleteRequest(
    GraphDeviceLoginChallengeResponse? Challenge);
