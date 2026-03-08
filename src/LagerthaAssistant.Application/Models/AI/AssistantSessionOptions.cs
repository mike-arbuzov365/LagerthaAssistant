namespace LagerthaAssistant.Application.Models.AI;

using LagerthaAssistant.Application.Constants;

public sealed class AssistantSessionOptions
{
    public string SystemPrompt { get; init; } = AssistantDefaults.SystemPrompt;

    public int MaxHistoryMessages { get; init; } = AssistantDefaults.MaxHistoryMessages;
}

