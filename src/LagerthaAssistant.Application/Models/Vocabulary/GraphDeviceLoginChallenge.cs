namespace LagerthaAssistant.Application.Models.Vocabulary;

public sealed record GraphDeviceLoginChallenge(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    int ExpiresInSeconds,
    int IntervalSeconds,
    DateTimeOffset ExpiresAtUtc,
    string? Message = null);
