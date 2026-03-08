namespace LagerthaAssistant.Application.Models.AI;

public sealed record AssistantCompletionResult(string Content, string Model, AssistantUsage? Usage);

