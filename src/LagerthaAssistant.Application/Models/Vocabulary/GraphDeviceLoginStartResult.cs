namespace LagerthaAssistant.Application.Models.Vocabulary;

public sealed record GraphDeviceLoginStartResult(
    bool Succeeded,
    string Message,
    GraphDeviceLoginChallenge? Challenge = null);
