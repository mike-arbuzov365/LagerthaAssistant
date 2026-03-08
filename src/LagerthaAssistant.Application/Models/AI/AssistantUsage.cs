namespace LagerthaAssistant.Application.Models.AI;

public sealed record AssistantUsage(int PromptTokens, int CompletionTokens, int TotalTokens);

