using System.Text.Json;

namespace LagerthaAssistant.Api.Contracts;

public sealed record MiniAppDiagnosticRequest(
    string SessionId,
    string EventType,
    string? Severity = null,
    string? Message = null,
    string? Path = null,
    bool? IsTelegram = null,
    string? HostSource = null,
    string? Platform = null,
    string? Channel = null,
    string? UserId = null,
    string? ConversationId = null,
    bool? HasInitData = null,
    bool? HasWebApp = null,
    string? Locale = null,
    IReadOnlyDictionary<string, JsonElement>? Details = null);
