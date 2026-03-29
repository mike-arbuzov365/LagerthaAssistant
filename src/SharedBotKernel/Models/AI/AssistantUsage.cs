namespace SharedBotKernel.Models.AI;

public sealed record AssistantUsage(int PromptTokens, int CompletionTokens, int TotalTokens);
