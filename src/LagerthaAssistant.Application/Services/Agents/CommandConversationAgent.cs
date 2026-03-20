namespace LagerthaAssistant.Application.Services.Agents;

using System.Text;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;

public sealed class CommandConversationAgent : IConversationAgent, IConversationAgentProfile
{
    private const int DefaultPreviewTake = 20;
    private const int DefaultSyncRunTake = ConversationCommandDefaults.SyncRunTake;
    private const int DefaultSyncFailedTake = ConversationCommandDefaults.SyncFailedPreviewTake;
    private const int DefaultSyncRetryFailedTake = ConversationCommandDefaults.SyncRetryFailedTake;

    private readonly IConversationIntentRouter _intentRouter;
    private readonly IConversationCommandCatalogService _commandCatalogService;
    private readonly IAssistantSessionService _assistantSessionService;
    private readonly IVocabularySyncProcessor _vocabularySyncProcessor;
    private readonly IVocabularyIndexService? _vocabularyIndexService;
    private readonly IVocabularyDeckService? _vocabularyDeckService;
    private readonly IVocabularyStorageModeProvider? _storageModeProvider;

    public CommandConversationAgent(
        IConversationIntentRouter intentRouter,
        IConversationCommandCatalogService commandCatalogService,
        IAssistantSessionService assistantSessionService,
        IVocabularySyncProcessor vocabularySyncProcessor,
        IVocabularyIndexService? vocabularyIndexService = null,
        IVocabularyDeckService? vocabularyDeckService = null,
        IVocabularyStorageModeProvider? storageModeProvider = null)
    {
        _intentRouter = intentRouter;
        _commandCatalogService = commandCatalogService;
        _assistantSessionService = assistantSessionService;
        _vocabularySyncProcessor = vocabularySyncProcessor;
        _vocabularyIndexService = vocabularyIndexService;
        _vocabularyDeckService = vocabularyDeckService;
        _storageModeProvider = storageModeProvider;
    }

    public string Name => "command-agent";

    public int Order => 10;

    public ConversationAgentRole Role => ConversationAgentRole.Command;

    public bool SupportsSlashCommands => true;

    public bool SupportsBatchInputs => false;

    public bool CanHandle(ConversationAgentContext context)
    {
        if (context.Input.StartsWith(ConversationInputMarkers.Chat, StringComparison.Ordinal))
        {
            return false;
        }

        if (context.HasResolvedCommandIntent)
        {
            return true;
        }

        return _intentRouter.TryResolve(context.Input, out _);
    }

    public async Task<ConversationAgentResult> HandleAsync(ConversationAgentContext context, CancellationToken cancellationToken = default)
    {
        var intent = context.ResolvedIntent;
        if (intent is null || intent.Type == ConversationCommandIntentType.Unsupported)
        {
            _intentRouter.TryResolve(context.Input, out intent);
        }

        return intent!.Type switch
        {
            ConversationCommandIntentType.Help => ConversationAgentResult.Empty(Name, "command.help", BuildHelpMessage()),
            ConversationCommandIntentType.History => await BuildHistoryResultAsync(cancellationToken),
            ConversationCommandIntentType.Memory => await BuildMemoryResultAsync(cancellationToken),
            ConversationCommandIntentType.PromptShow => await BuildPromptResultAsync(cancellationToken),
            ConversationCommandIntentType.PromptResetDefault => await ResetPromptResultAsync(cancellationToken),
            ConversationCommandIntentType.PromptHistory => await BuildPromptHistoryResultAsync(cancellationToken),
            ConversationCommandIntentType.PromptProposals => await BuildPromptProposalsResultAsync(cancellationToken),
            ConversationCommandIntentType.PromptSet => await SetPromptResultAsync(intent, cancellationToken),
            ConversationCommandIntentType.PromptPropose => await ProposePromptResultAsync(intent, cancellationToken),
            ConversationCommandIntentType.PromptImprove => await ImprovePromptResultAsync(intent, cancellationToken),
            ConversationCommandIntentType.PromptApply => await ApplyPromptResultAsync(intent, cancellationToken),
            ConversationCommandIntentType.PromptReject => await RejectPromptResultAsync(intent, cancellationToken),
            ConversationCommandIntentType.SyncStatus => await BuildSyncStatusResultAsync(cancellationToken),
            ConversationCommandIntentType.SyncFailed => await BuildSyncFailedResultAsync(intent.Number ?? DefaultSyncFailedTake, cancellationToken),
            ConversationCommandIntentType.SyncRun => await BuildSyncRunResultAsync(intent.Number ?? DefaultSyncRunTake, cancellationToken),
            ConversationCommandIntentType.SyncRetryFailed => await BuildSyncRetryFailedResultAsync(intent.Number ?? DefaultSyncRetryFailedTake, cancellationToken),
            ConversationCommandIntentType.ResetConversation => ResetConversationResult(),
            ConversationCommandIntentType.IndexHelp => BuildIndexHelpResult(),
            ConversationCommandIntentType.IndexClear => await BuildIndexClearResultAsync(cancellationToken),
            ConversationCommandIntentType.IndexRebuild => await BuildIndexRebuildResultAsync(cancellationToken),
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

    private async Task<ConversationAgentResult> BuildPromptHistoryResultAsync(CancellationToken cancellationToken)
    {
        var history = await _assistantSessionService.GetSystemPromptHistoryAsync(DefaultPreviewTake, cancellationToken);
        if (history.Count == 0)
        {
            return ConversationAgentResult.Empty(Name, "command.prompt.history", "System prompt history is empty.");
        }

        var message = string.Join(
            Environment.NewLine,
            history.SelectMany(item =>
            {
                var activeFlag = item.IsActive ? " [active]" : string.Empty;
                return new[]
                {
                    $"- v{item.Version}{activeFlag} source={item.Source} created={item.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC",
                    $"  {item.PromptText}"
                };
            }));

        return ConversationAgentResult.Empty(Name, "command.prompt.history", message);
    }

    private async Task<ConversationAgentResult> BuildPromptProposalsResultAsync(CancellationToken cancellationToken)
    {
        var proposals = await _assistantSessionService.GetSystemPromptProposalsAsync(DefaultPreviewTake, cancellationToken);
        if (proposals.Count == 0)
        {
            return ConversationAgentResult.Empty(Name, "command.prompt.proposals", "System prompt proposals are empty.");
        }

        var message = string.Join(
            Environment.NewLine,
            proposals.SelectMany(item =>
                new[]
                {
                    $"- #{item.Id} status={item.Status} source={item.Source} confidence={item.Confidence:F2} created={item.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC",
                    $"  reason: {item.Reason}",
                    $"  prompt: {item.ProposedPrompt}"
                }));

        return ConversationAgentResult.Empty(Name, "command.prompt.proposals", message);
    }


    private async Task<ConversationAgentResult> SetPromptResultAsync(ConversationCommandIntent intent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(intent.Argument))
        {
            return ConversationAgentResult.Empty(Name, "command.prompt.set", $"Usage: {ConversationSlashCommands.PromptSet} <new prompt text>");
        }

        var updatedPrompt = await _assistantSessionService.SetSystemPromptAsync(intent.Argument, "manual", cancellationToken);
        var message = new StringBuilder()
            .AppendLine("System prompt updated and saved.")
            .AppendLine()
            .Append(updatedPrompt)
            .ToString();

        return ConversationAgentResult.Empty(Name, "command.prompt.set", message);
    }

    private async Task<ConversationAgentResult> ProposePromptResultAsync(ConversationCommandIntent intent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(intent.Argument) || string.IsNullOrWhiteSpace(intent.Argument2))
        {
            return ConversationAgentResult.Empty(Name, "command.prompt.propose", $"Usage: {ConversationSlashCommands.PromptPropose} <reason> || <new prompt text>");
        }

        var proposal = await _assistantSessionService.CreateSystemPromptProposalAsync(
            intent.Argument2,
            intent.Argument,
            0.8,
            "manual",
            cancellationToken);

        return ConversationAgentResult.Empty(
            Name,
            "command.prompt.propose",
            $"Proposal #{proposal.Id} has been saved with status '{proposal.Status}'.");
    }

    private async Task<ConversationAgentResult> ImprovePromptResultAsync(ConversationCommandIntent intent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(intent.Argument))
        {
            return ConversationAgentResult.Empty(Name, "command.prompt.improve", $"Usage: {ConversationSlashCommands.PromptImprove} <goal>");
        }

        var proposal = await _assistantSessionService.GenerateSystemPromptProposalAsync(intent.Argument, cancellationToken);

        return ConversationAgentResult.Empty(
            Name,
            "command.prompt.improve",
            $"AI proposal #{proposal.Id} generated. Review via {ConversationSlashCommands.PromptProposals} and apply with {ConversationSlashCommands.PromptApply} <id>.");
    }

    private async Task<ConversationAgentResult> ApplyPromptResultAsync(ConversationCommandIntent intent, CancellationToken cancellationToken)
    {
        if (!intent.Number.HasValue || intent.Number.Value <= 0)
        {
            return ConversationAgentResult.Empty(Name, "command.prompt.apply", $"Usage: {ConversationSlashCommands.PromptApply} <proposalId>");
        }

        var updatedPrompt = await _assistantSessionService.ApplySystemPromptProposalAsync(intent.Number.Value, cancellationToken);
        var message = new StringBuilder()
            .AppendLine($"Proposal #{intent.Number.Value} applied.")
            .AppendLine()
            .Append(updatedPrompt)
            .ToString();

        return ConversationAgentResult.Empty(Name, "command.prompt.apply", message);
    }

    private async Task<ConversationAgentResult> RejectPromptResultAsync(ConversationCommandIntent intent, CancellationToken cancellationToken)
    {
        if (!intent.Number.HasValue || intent.Number.Value <= 0)
        {
            return ConversationAgentResult.Empty(Name, "command.prompt.reject", $"Usage: {ConversationSlashCommands.PromptReject} <proposalId>");
        }

        await _assistantSessionService.RejectSystemPromptProposalAsync(intent.Number.Value, cancellationToken);
        return ConversationAgentResult.Empty(Name, "command.prompt.reject", $"Proposal #{intent.Number.Value} rejected.");
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

    private async Task<ConversationAgentResult> BuildSyncFailedResultAsync(int take, CancellationToken cancellationToken)
    {
        var safeTake = Math.Clamp(take, 1, 500);
        var failed = await _vocabularySyncProcessor.GetFailedJobsAsync(safeTake, cancellationToken);
        if (failed.Count == 0)
        {
            return ConversationAgentResult.Empty(Name, "command.sync.failed", "Failed vocabulary sync jobs: 0.");
        }

        var lines = failed.Select(job =>
            $"- #{job.Id} word='{job.RequestedWord}' deck='{job.TargetDeckFileName}' mode={job.StorageMode} attempts={job.AttemptCount} last='{job.LastError ?? "n/a"}'");
        var message = string.Join(Environment.NewLine, lines);
        return ConversationAgentResult.Empty(Name, "command.sync.failed", message);
    }

    private async Task<ConversationAgentResult> BuildSyncRetryFailedResultAsync(int take, CancellationToken cancellationToken)
    {
        var safeTake = Math.Clamp(take, 1, 500);
        var requeued = await _vocabularySyncProcessor.RequeueFailedAsync(safeTake, cancellationToken);
        return ConversationAgentResult.Empty(Name, "command.sync.retry-failed", $"Requeued failed vocabulary sync jobs: {requeued}.");
    }

    private ConversationAgentResult ResetConversationResult()
    {
        _assistantSessionService.Reset();
        return ConversationAgentResult.Empty(Name, "command.reset", "Conversation has been reset.");
    }

    private ConversationAgentResult BuildIndexHelpResult()
    {
        var lines = new[]
        {
            "Vocabulary index commands:",
            $"- {ConversationSlashCommands.IndexClear} - clear SQL vocabulary index.",
            $"- {ConversationSlashCommands.IndexRebuild} - rebuild SQL vocabulary index from decks.",
            "Note: index commands update SQL only and do not write cards to Excel/OneDrive decks.",
            $"To flush pending deck writes, run {ConversationSlashCommands.SyncRun}."
        };

        return ConversationAgentResult.Empty(Name, "command.index.help", string.Join(Environment.NewLine, lines));
    }

    private async Task<ConversationAgentResult> BuildIndexClearResultAsync(CancellationToken cancellationToken)
    {
        if (_vocabularyIndexService is null)
        {
            return ConversationAgentResult.Empty(Name, "command.index.clear", "Index service is not available.");
        }

        var deleted = await _vocabularyIndexService.ClearAsync(cancellationToken);
        return ConversationAgentResult.Empty(Name, "command.index.clear", $"Index cleared: {deleted} card(s) removed.");
    }

    private async Task<ConversationAgentResult> BuildIndexRebuildResultAsync(CancellationToken cancellationToken)
    {
        if (_vocabularyIndexService is null || _vocabularyDeckService is null)
        {
            return ConversationAgentResult.Empty(Name, "command.index.rebuild", "Index or deck service is not available.");
        }

        var entries = await _vocabularyDeckService.GetAllEntriesAsync(cancellationToken);
        var storageMode = _storageModeProvider?.CurrentMode ?? VocabularyStorageMode.Local;
        var indexed = await _vocabularyIndexService.RebuildAsync(entries, storageMode, cancellationToken);

        return ConversationAgentResult.Empty(
            Name,
            "command.index.rebuild",
            $"Index rebuilt: {entries.Count} entry/entries scanned, {indexed} card(s) indexed.");
    }

    private string BuildHelpMessage()
    {
        var lines = new List<string>
        {
            "Available slash commands:"
        };

        var groups = _commandCatalogService.GetGroups();

        foreach (var group in groups)
        {
            lines.Add($"{group.Category}:");
            lines.AddRange(group.Commands.Select(item => $"- {item.Command} - {item.Description}"));
            lines.Add(string.Empty);
        }

        if (lines.Count > 0 && string.IsNullOrEmpty(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        lines.Add("Natural-language command intents are also supported (for example: show history, show memory, reset conversation).");

        return string.Join(Environment.NewLine, lines);
    }
}
