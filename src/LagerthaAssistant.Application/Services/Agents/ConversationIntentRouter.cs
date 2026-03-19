namespace LagerthaAssistant.Application.Services.Agents;

using System.Text.RegularExpressions;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Models.Agents;

public sealed class ConversationIntentRouter : IConversationIntentRouter
{
    private static readonly Regex SyncRunNumberRegex = new(
        "(?:sync\\s+run|run\\s+sync|process\\s+sync|retry\\s+sync)\\s+(?<n>\\d{1,4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SyncRetryFailedNumberRegex = new(
        "(?:sync\\s+retry\\s+failed|retry\\s+failed\\s+sync|requeue\\s+failed\\s+sync)\\s+(?<n>\\d{1,4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool TryResolve(string input, out ConversationCommandIntent intent)
    {
        var normalized = Normalize(input);
        var raw = input?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.Unsupported);
            return false;
        }

        if (TryResolveSlash(normalized, raw, out intent))
        {
            return true;
        }

        // Avoid hijacking single-word vocabulary lookups.
        if (normalized.IndexOf(' ') < 0)
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.Unsupported, Raw: raw);
            return false;
        }

        if (TryResolveNatural(normalized, out intent))
        {
            return true;
        }

        intent = new ConversationCommandIntent(ConversationCommandIntentType.Unsupported, Raw: raw);
        return false;
    }

    private static bool TryResolveSlash(string normalized, string raw, out ConversationCommandIntent intent)
    {
        if (!raw.StartsWith("/", StringComparison.Ordinal))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.Unsupported);
            return false;
        }

        if (normalized.Equals(ConversationSlashCommands.Help, StringComparison.Ordinal))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.Help, Raw: raw);
            return true;
        }

        if (normalized.Equals(ConversationSlashCommands.History, StringComparison.Ordinal))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.History, Raw: raw);
            return true;
        }

        if (normalized.Equals(ConversationSlashCommands.Memory, StringComparison.Ordinal))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.Memory, Raw: raw);
            return true;
        }

        if (normalized.Equals(ConversationSlashCommands.Prompt, StringComparison.Ordinal))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.PromptShow, Raw: raw);
            return true;
        }

        if (normalized.Equals(ConversationSlashCommands.PromptDefault, StringComparison.Ordinal))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.PromptResetDefault, Raw: raw);
            return true;
        }

        if (normalized.Equals(ConversationSlashCommands.PromptHistory, StringComparison.Ordinal))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.PromptHistory, Raw: raw);
            return true;
        }

        if (normalized.Equals(ConversationSlashCommands.PromptProposals, StringComparison.Ordinal))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.PromptProposals, Raw: raw);
            return true;
        }

        const string promptSetPrefix = ConversationSlashCommands.PromptSet;
        if (raw.Equals(promptSetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.PromptSet, Raw: raw);
            return true;
        }

        if (raw.StartsWith(promptSetPrefix + " ", StringComparison.OrdinalIgnoreCase))
        {
            var promptText = raw[promptSetPrefix.Length..].TrimStart();
            intent = new ConversationCommandIntent(ConversationCommandIntentType.PromptSet, Raw: raw, Argument: promptText);
            return true;
        }

        const string promptProposePrefix = ConversationSlashCommands.PromptPropose;
        if (raw.Equals(promptProposePrefix, StringComparison.OrdinalIgnoreCase))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.PromptPropose, Raw: raw);
            return true;
        }

        if (raw.StartsWith(promptProposePrefix + " ", StringComparison.OrdinalIgnoreCase))
        {
            var payload = raw[promptProposePrefix.Length..].TrimStart();
            var split = payload.Split("||", 2, StringSplitOptions.TrimEntries);
            var reason = split.Length > 0 ? split[0] : string.Empty;
            var proposedPrompt = split.Length > 1 ? split[1] : string.Empty;
            intent = new ConversationCommandIntent(
                ConversationCommandIntentType.PromptPropose,
                Raw: raw,
                Argument: reason,
                Argument2: proposedPrompt);
            return true;
        }

        const string promptImprovePrefix = ConversationSlashCommands.PromptImprove;
        if (raw.Equals(promptImprovePrefix, StringComparison.OrdinalIgnoreCase))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.PromptImprove, Raw: raw);
            return true;
        }

        if (raw.StartsWith(promptImprovePrefix + " ", StringComparison.OrdinalIgnoreCase))
        {
            var goal = raw[promptImprovePrefix.Length..].TrimStart();
            intent = new ConversationCommandIntent(ConversationCommandIntentType.PromptImprove, Raw: raw, Argument: goal);
            return true;
        }

        const string promptApplyPrefix = ConversationSlashCommands.PromptApply;
        if (raw.Equals(promptApplyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.PromptApply, Raw: raw);
            return true;
        }

        if (raw.StartsWith(promptApplyPrefix + " ", StringComparison.OrdinalIgnoreCase))
        {
            var idText = raw[promptApplyPrefix.Length..].TrimStart();
            var parsed = int.TryParse(idText, out var proposalId) && proposalId > 0 ? proposalId : (int?)null;
            intent = new ConversationCommandIntent(ConversationCommandIntentType.PromptApply, Number: parsed, Raw: raw);
            return true;
        }

        const string promptRejectPrefix = ConversationSlashCommands.PromptReject;
        if (raw.Equals(promptRejectPrefix, StringComparison.OrdinalIgnoreCase))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.PromptReject, Raw: raw);
            return true;
        }

        if (raw.StartsWith(promptRejectPrefix + " ", StringComparison.OrdinalIgnoreCase))
        {
            var idText = raw[promptRejectPrefix.Length..].TrimStart();
            var parsed = int.TryParse(idText, out var proposalId) && proposalId > 0 ? proposalId : (int?)null;
            intent = new ConversationCommandIntent(ConversationCommandIntentType.PromptReject, Number: parsed, Raw: raw);
            return true;
        }

        if (normalized.Equals(ConversationSlashCommands.Sync, StringComparison.Ordinal)
            || normalized.Equals(ConversationSlashCommands.SyncStatus, StringComparison.Ordinal))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.SyncStatus, Raw: raw);
            return true;
        }

        if (normalized.Equals(ConversationSlashCommands.SyncFailed, StringComparison.Ordinal))
        {
            intent = new ConversationCommandIntent(
                ConversationCommandIntentType.SyncFailed,
                Number: ConversationCommandDefaults.SyncFailedPreviewTake,
                Raw: raw);
            return true;
        }

        if (normalized.Equals(ConversationSlashCommands.SyncRun, StringComparison.Ordinal))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.SyncRun, Number: ConversationCommandDefaults.SyncRunTake, Raw: raw);
            return true;
        }

        if (normalized.Equals(ConversationSlashCommands.SyncRetryFailed, StringComparison.Ordinal))
        {
            intent = new ConversationCommandIntent(
                ConversationCommandIntentType.SyncRetryFailed,
                Number: ConversationCommandDefaults.SyncRetryFailedTake,
                Raw: raw);
            return true;
        }

        if (normalized.StartsWith(ConversationSlashCommands.SyncRun + " ", StringComparison.Ordinal)
            && int.TryParse(normalized[(ConversationSlashCommands.SyncRun.Length + 1)..].Trim(), out var take)
            && take > 0)
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.SyncRun, Number: take, Raw: raw);
            return true;
        }

        if (normalized.StartsWith(ConversationSlashCommands.SyncRetryFailed + " ", StringComparison.Ordinal)
            && int.TryParse(normalized[(ConversationSlashCommands.SyncRetryFailed.Length + 1)..].Trim(), out var retryTake)
            && retryTake > 0)
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.SyncRetryFailed, Number: retryTake, Raw: raw);
            return true;
        }

        if (normalized.Equals(ConversationSlashCommands.Reset, StringComparison.Ordinal))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.ResetConversation, Raw: raw);
            return true;
        }

        if (normalized.Equals(ConversationSlashCommands.Index, StringComparison.Ordinal))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.IndexHelp, Raw: raw);
            return true;
        }

        if (normalized.Equals(ConversationSlashCommands.IndexClear, StringComparison.Ordinal))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.IndexClear, Raw: raw);
            return true;
        }

        if (normalized.Equals(ConversationSlashCommands.IndexRebuild, StringComparison.Ordinal))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.IndexRebuild, Raw: raw);
            return true;
        }

        intent = new ConversationCommandIntent(ConversationCommandIntentType.Unsupported, Raw: raw);
        return true;
    }

    private static bool TryResolveNatural(string normalized, out ConversationCommandIntent intent)
    {
        if (IsHelpIntent(normalized))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.Help, Raw: normalized);
            return true;
        }

        if (IsHistoryIntent(normalized))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.History, Raw: normalized);
            return true;
        }

        if (IsMemoryIntent(normalized))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.Memory, Raw: normalized);
            return true;
        }

        if (IsPromptResetIntent(normalized))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.PromptResetDefault, Raw: normalized);
            return true;
        }

        if (IsPromptShowIntent(normalized))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.PromptShow, Raw: normalized);
            return true;
        }

        if (TryParseSyncRetryFailedIntent(normalized, out var retryFailedIntent))
        {
            intent = retryFailedIntent;
            return true;
        }

        if (TryParseSyncRunIntent(normalized, out var runIntent))
        {
            intent = runIntent;
            return true;
        }

        if (IsSyncFailedIntent(normalized))
        {
            intent = new ConversationCommandIntent(
                ConversationCommandIntentType.SyncFailed,
                Number: ConversationCommandDefaults.SyncFailedPreviewTake,
                Raw: normalized);
            return true;
        }

        if (IsSyncStatusIntent(normalized))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.SyncStatus, Raw: normalized);
            return true;
        }

        if (IsResetIntent(normalized))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.ResetConversation, Raw: normalized);
            return true;
        }

        intent = new ConversationCommandIntent(ConversationCommandIntentType.Unsupported, Raw: normalized);
        return false;
    }

    private static bool IsHelpIntent(string value)
    {
        return value.Contains("help", StringComparison.Ordinal)
            || value.Contains("what can you do", StringComparison.Ordinal)
            || value.Contains("available commands", StringComparison.Ordinal)
            || value.Contains("show commands", StringComparison.Ordinal)
            || value.Contains("\u0434\u043e\u043f\u043e\u043c", StringComparison.Ordinal)
            || value.Contains("\u0449\u043e \u0442\u0438 \u0432\u043c\u0456\u0454\u0448", StringComparison.Ordinal)
            || value.Contains("\u044f\u043a\u0456 \u043a\u043e\u043c\u0430\u043d\u0434\u0438", StringComparison.Ordinal);
    }

    private static bool IsHistoryIntent(string value)
    {
        return HasActionCue(value)
            && (value.Contains("history", StringComparison.Ordinal)
                || value.Contains("conversation history", StringComparison.Ordinal)
                || value.Contains("\u0456\u0441\u0442\u043e\u0440", StringComparison.Ordinal));
    }

    private static bool IsMemoryIntent(string value)
    {
        return HasActionCue(value)
            && (value.Contains("memory", StringComparison.Ordinal)
                || value.Contains("active memory", StringComparison.Ordinal)
                || value.Contains("\u043f\u0430\u043c'\u044f\u0442\u044c", StringComparison.Ordinal)
                || value.Contains("\u043f\u0430\u043c\u044f\u0442", StringComparison.Ordinal));
    }

    private static bool IsPromptShowIntent(string value)
    {
        return HasActionCue(value)
            && (value.Contains("system prompt", StringComparison.Ordinal)
                || value.Contains("current prompt", StringComparison.Ordinal)
                || value.Contains("show prompt", StringComparison.Ordinal)
                || value.Contains("\u0441\u0438\u0441\u0442\u0435\u043c\u043d\u0438\u0439 \u043f\u0440\u043e\u043c\u043f\u0442", StringComparison.Ordinal)
                || value.Contains("\u043f\u043e\u043a\u0430\u0436\u0438 \u043f\u0440\u043e\u043c\u043f\u0442", StringComparison.Ordinal));
    }

    private static bool IsPromptResetIntent(string value)
    {
        var hasPrompt = value.Contains("prompt", StringComparison.Ordinal)
            || value.Contains("\u043f\u0440\u043e\u043c\u043f\u0442", StringComparison.Ordinal);

        var hasReset = value.Contains("reset", StringComparison.Ordinal)
            || value.Contains("default", StringComparison.Ordinal)
            || value.Contains("restore", StringComparison.Ordinal)
            || value.Contains("\u0441\u043a\u0438\u043d\u044c", StringComparison.Ordinal)
            || value.Contains("\u0432\u0456\u0434\u043d\u043e\u0432", StringComparison.Ordinal)
            || value.Contains("\u0434\u0435\u0444\u043e\u043b\u0442", StringComparison.Ordinal);

        return hasPrompt && hasReset;
    }

    private static bool IsSyncStatusIntent(string value)
    {
        var hasSync = value.Contains("sync", StringComparison.Ordinal)
            || value.Contains("queue", StringComparison.Ordinal)
            || value.Contains("\u0447\u0435\u0440\u0433\u0430", StringComparison.Ordinal)
            || value.Contains("\u0441\u0438\u043d\u0445", StringComparison.Ordinal);

        var asksStatus = value.Contains("status", StringComparison.Ordinal)
            || value.Contains("pending", StringComparison.Ordinal)
            || value.Contains("\u0441\u043a\u0456\u043b\u044c\u043a\u0438", StringComparison.Ordinal)
            || value.Contains("\u0441\u0442\u0430\u043d", StringComparison.Ordinal)
            || value.Contains("show", StringComparison.Ordinal)
            || value.Contains("\u043f\u043e\u043a\u0430\u0436\u0438", StringComparison.Ordinal);

        return hasSync && asksStatus;
    }

    private static bool IsSyncFailedIntent(string value)
    {
        var hasSync = value.Contains("sync", StringComparison.Ordinal)
            || value.Contains("queue", StringComparison.Ordinal)
            || value.Contains("\u0447\u0435\u0440\u0433\u0430", StringComparison.Ordinal)
            || value.Contains("\u0441\u0438\u043d\u0445", StringComparison.Ordinal);

        var hasFailed = value.Contains("failed", StringComparison.Ordinal)
            || value.Contains("error", StringComparison.Ordinal)
            || value.Contains("\u043f\u043e\u043c\u0438\u043b", StringComparison.Ordinal)
            || value.Contains("\u043d\u0435\u0432\u0434\u0430\u043b", StringComparison.Ordinal);

        var asksStatus = value.Contains("status", StringComparison.Ordinal)
            || value.Contains("show", StringComparison.Ordinal)
            || value.Contains("list", StringComparison.Ordinal)
            || value.Contains("\u043f\u043e\u043a\u0430\u0436\u0438", StringComparison.Ordinal)
            || value.Contains("\u0441\u043f\u0438\u0441\u043e\u043a", StringComparison.Ordinal);

        return hasSync && hasFailed && asksStatus;
    }

    private static bool TryParseSyncRunIntent(string value, out ConversationCommandIntent intent)
    {
        var hasSync = value.Contains("sync", StringComparison.Ordinal)
            || value.Contains("\u0441\u0438\u043d\u0445", StringComparison.Ordinal);

        var hasRun = value.Contains("run", StringComparison.Ordinal)
            || value.Contains("process", StringComparison.Ordinal)
            || value.Contains("retry", StringComparison.Ordinal)
            || value.Contains("start", StringComparison.Ordinal)
            || value.Contains("\u0437\u0430\u043f\u0443\u0441\u0442\u0438", StringComparison.Ordinal)
            || value.Contains("\u043e\u0431\u0440\u043e\u0431", StringComparison.Ordinal)
            || value.Contains("\u043f\u043e\u0432\u0442\u043e\u0440\u0438", StringComparison.Ordinal);

        if (!hasSync || !hasRun)
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.Unsupported);
            return false;
        }

        var match = SyncRunNumberRegex.Match(value);
        if (match.Success && int.TryParse(match.Groups["n"].Value, out var parsed) && parsed > 0)
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.SyncRun, parsed, value);
            return true;
        }

        intent = new ConversationCommandIntent(ConversationCommandIntentType.SyncRun, ConversationCommandDefaults.SyncRunTake, value);
        return true;
    }

    private static bool TryParseSyncRetryFailedIntent(string value, out ConversationCommandIntent intent)
    {
        var hasSync = value.Contains("sync", StringComparison.Ordinal)
            || value.Contains("\u0441\u0438\u043d\u0445", StringComparison.Ordinal);

        var hasFailed = value.Contains("failed", StringComparison.Ordinal)
            || value.Contains("error", StringComparison.Ordinal)
            || value.Contains("\u043f\u043e\u043c\u0438\u043b", StringComparison.Ordinal)
            || value.Contains("\u043d\u0435\u0432\u0434\u0430\u043b", StringComparison.Ordinal);

        var hasRetry = value.Contains("retry", StringComparison.Ordinal)
            || value.Contains("requeue", StringComparison.Ordinal)
            || value.Contains("\u043f\u043e\u0432\u0442\u043e\u0440", StringComparison.Ordinal)
            || value.Contains("\u043f\u0435\u0440\u0435\u0437\u0430\u043f\u0443\u0441\u0442", StringComparison.Ordinal);

        if (!hasSync || !hasFailed || !hasRetry)
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.Unsupported);
            return false;
        }

        var match = SyncRetryFailedNumberRegex.Match(value);
        if (match.Success && int.TryParse(match.Groups["n"].Value, out var parsed) && parsed > 0)
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.SyncRetryFailed, parsed, value);
            return true;
        }

        intent = new ConversationCommandIntent(ConversationCommandIntentType.SyncRetryFailed, ConversationCommandDefaults.SyncRetryFailedTake, value);
        return true;
    }

    private static bool IsResetIntent(string value)
    {
        var hasResetVerb = value.Contains("reset", StringComparison.Ordinal)
            || value.Contains("clear", StringComparison.Ordinal)
            || value.Contains("new session", StringComparison.Ordinal)
            || value.Contains("\u0441\u043a\u0438\u043d\u044c", StringComparison.Ordinal)
            || value.Contains("\u043e\u0447\u0438\u0441\u0442", StringComparison.Ordinal)
            || value.Contains("\u043d\u043e\u0432\u0443 \u0441\u0435\u0441\u0456\u044e", StringComparison.Ordinal)
            || value.Contains("\u043d\u043e\u0432\u0430 \u0441\u0435\u0441\u0456\u044f", StringComparison.Ordinal);

        var hasTarget = value.Contains("conversation", StringComparison.Ordinal)
            || value.Contains("session", StringComparison.Ordinal)
            || value.Contains("context", StringComparison.Ordinal)
            || value.Contains("\u043a\u043e\u043d\u0442\u0435\u043a\u0441\u0442", StringComparison.Ordinal)
            || value.Contains("\u0441\u0435\u0441\u0456", StringComparison.Ordinal)
            || value.Contains("\u0440\u043e\u0437\u043c\u043e\u0432", StringComparison.Ordinal);

        return hasResetVerb && hasTarget;
    }

    private static bool HasActionCue(string value)
    {
        return value.Contains("show", StringComparison.Ordinal)
            || value.Contains("get", StringComparison.Ordinal)
            || value.Contains("display", StringComparison.Ordinal)
            || value.Contains("what is", StringComparison.Ordinal)
            || value.Contains("\u043f\u043e\u043a\u0430\u0436\u0438", StringComparison.Ordinal)
            || value.Contains("\u0432\u0438\u0432\u0435\u0434\u0438", StringComparison.Ordinal)
            || value.Contains("\u0434\u0430\u0439", StringComparison.Ordinal);
    }

    private static string Normalize(string input)
    {
        return Regex.Replace(input?.Trim().ToLowerInvariant() ?? string.Empty, "\\s+", " ");
    }
}
