namespace LagerthaAssistant.Api.Contracts;

public sealed record MiniAppAuthVerifyRequest(string InitData);

public sealed record MiniAppAuthVerifyResponse(
    bool IsValid,
    string Reason,
    DateTimeOffset? AuthDateUtc);
