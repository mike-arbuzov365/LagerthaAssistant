namespace LagerthaAssistant.Application.Models.Vocabulary;

public sealed record GraphDeviceCodePrompt(
    string UserCode,
    string VerificationUri,
    string? Message = null);
