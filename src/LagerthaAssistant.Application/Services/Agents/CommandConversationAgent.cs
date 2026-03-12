namespace LagerthaAssistant.Application.Services.Agents;

using System.Text;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;

public sealed class CommandConversationAgent : IConversationAgent
{
    private const int DefaultPreviewTake = 20;
    private const int DefaultSyncRunTake = 25;

    private readonly IConversationIntentRouter _intentRouter;
    private readonly IAssistantSessionService _assistantSessionService;
    private readonly IVocabularySyncProcessor _vocabularySyncProcessor;

    public CommandConversationAgent(
        IConversationIntentRouter intentRouter,
        IAssistantSessionService assistantSessionService,
        IVocabularySyncProcessor vocabularySyncProcessor)
    {
        _intentRouter = intentRouter;
        _assistantSessionService = assistantSessionService;
        _vocabularySyncProcessor = vocabularySyncProcessor;
    }

    public string Name => "command-agent";

    public int Order => 10;

    public bool CanHandle(ConversationAgentContext context)
        => _intentRouter.TryResolve(context.Input, out _);

    public async Task<ConversationAgentResult> HandleAsync(ConversationAgentContext context, CancellationToken cancellationToken = default)
    {
        _intentRouter.TryResolve(context.Input, out var intent);

        return intent.Type switch
        {
            ConversationCommandIntentType.Help => ConversationAgentResult.Empty(Name, "command.help", BuildHelpMessage()),
            ConversationCommandIntentType.History => await BuildHistoryResultAsync(cancellationToken),
            ConversationCommandIntentType.Memory => await BuildMemoryResultAsync(cancellationToken),
            ConversationCommandIntentType.PromptShow => await BuildPromptResultAsync(cancellationToken),
            ConversationCommandIntentType.PromptResetDefault => await ResetPromptResultAsync(cancellationToken),
            ConversationCommandIntentType.SyncStatus => await BuildSyncStatusResultAsync(cancellationToken),
            ConversationCommandIntentType.SyncRun => await BuildSyncRunResultAsync(intent.Number ?? DefaultSyncRunTake, cancellationToken),
            ConversationCommandIntentType.ResetConversation => ResetConversationResult(),
            _ => ConversationAgentResult.Empty(
                Name,
                "command.unsupported",
                "Unsupported command in API mode. Use natural language for vocabulary or ask for help.")
        };
    }

    private async Task<ConversationAgentResult> BuildHistoryResultAsync(CancellationToken cancellationToken)
    {
        var history = await _assistantSessionService.GetRecentHistoryAsync(DefaultPreviewTake, cancellationToken);
        if (history.Count == 0)
        {
            return ConversationAgentResult.Empty(Name, "command.history", "Conversation history is empty.");
        }

        var message = string.Join(
            Environment.NewLine,
            history.Select(entry => $"- {entry.Role}: {entry.Content}"));

        return ConversationAgentResult.Empty(Name, "command.history", message);
    }

    private async Task<ConversationAgentResult> BuildMemoryResultAsync(CancellationToken cancellationToken)
    {
        var memory = await _assistantSessionService.GetActiveMemoryAsync(DefaultPreviewTake, cancellationToken);
        if (memory.Count == 0)
        {
            return ConversationAgentResult.Empty(Name, "command.memory", "Active memory is empty.");
        }

        var message = string.Join(
            Environment.NewLine,
            memory.Select(entry => $"- {entry.Key}: {entry.Value} (confidence={entry.Confidence:F2})"));

        return ConversationAgentResult.Empty(Name, "command.memory", message);
    }

    private async Task<ConversationAgentResult> BuildPromptResultAsync(CancellationToken cancellationToken)
    {
        var prompt = await _assistantSessionService.GetSystemPromptAsync(cancellationToken);
        return ConversationAgentResult.Empty(Name, "command.prompt.show", prompt);
    }

    private async Task<ConversationAgentResult> ResetPromptResultAsync(CancellationToken cancellationToken)
    {
        var updatedPrompt = await _assistantSessionService.SetSystemPromptAsync(AssistantDefaults.SystemPrompt, "default", cancellationToken);

        var message = new StringBuilder()
            .AppendLine("System prompt reset to default and saved.")
            .AppendLine()
            .Append(updatedPrompt)
            .ToString();

        return ConversationAgentResult.Empty(Name, "command.prompt.default", message);
    }

    private async Task<ConversationAgentResult> BuildSyncStatusResultAsync(CancellationToken cancellationToken)
    {
        var pending = await _vocabularySyncProcessor.GetPendingCountAsync(cancellationToken);
        return ConversationAgentResult.Empty(Name, "command.sync.status", $"Pending vocabulary sync jobs: {pending}.");
    }

    private async Task<ConversationAgentResult> BuildSyncRunResultAsync(int take, CancellationToken cancellationToken)
    {
        var safeTake = Math.Clamp(take, 1, 500);
        var summary = await _vocabularySyncProcessor.ProcessPendingAsync(safeTake, cancellationToken);

        var message = $"Sync run processed {summary.Processed}/{summary.Requested} job(s): completed={summary.Completed}, requeued={summary.Requeued}, failed={summary.Failed}, pending={summary.PendingAfterRun}.";
        return ConversationAgentResult.Empty(Name, "command.sync.run", message);
    }

    private ConversationAgentResult ResetConversationResult()
    {
        _assistantSessionService.Reset();
        return ConversationAgentResult.Empty(Name, "command.reset", "Conversation has been reset.");
    }

    private static string BuildHelpMessage()
    {
        return string.Join(Environment.NewLine, new[]
        {
            "Available command intents:",
            "- show history",
            "- show memory",
            "- show system prompt",
            "- reset prompt to default",
            "- sync status",
            "- run sync [N]",
            "- reset conversation",
            "You can also use slash forms like /history, /prompt, /sync run 25."
        });
    }
}
