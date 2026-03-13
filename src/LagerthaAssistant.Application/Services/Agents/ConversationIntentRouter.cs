namespace LagerthaAssistant.Application.Services.Agents;

using System.Text.RegularExpressions;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Models.Agents;

public sealed class ConversationIntentRouter : IConversationIntentRouter
{
    private static readonly Regex SyncRunNumberRegex = new("(?:sync\\s+run|run\\s+sync|process\\s+sync|retry\\s+sync)\\s+(?<n>\\d{1,4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

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

        if (normalized.Equals(ConversationSlashCommands.SyncRun, StringComparison.Ordinal))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.SyncRun, Number: ConversationCommandDefaults.SyncRunTake, Raw: raw);
            return true;
        }

        if (normalized.StartsWith(ConversationSlashCommands.SyncRun + " ", StringComparison.Ordinal)
            && int.TryParse(normalized[(ConversationSlashCommands.SyncRun.Length + 1)..].Trim(), out var take)
            && take > 0)
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.SyncRun, Number: take, Raw: raw);
            return true;
        }

        if (normalized.Equals(ConversationSlashCommands.Reset, StringComparison.Ordinal))
        {
            intent = new ConversationCommandIntent(ConversationCommandIntentType.ResetConversation, Raw: raw);
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

        if (TryParseSyncRunIntent(normalized, out var runIntent))
        {
            intent = runIntent;
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
            || value.Contains("допом", StringComparison.Ordinal)
            || value.Contains("що ти вмієш", StringComparison.Ordinal)
            || value.Contains("які команди", StringComparison.Ordinal);
    }

    private static bool IsHistoryIntent(string value)
    {
        return HasActionCue(value)
            && (value.Contains("history", StringComparison.Ordinal)
                || value.Contains("conversation history", StringComparison.Ordinal)
                || value.Contains("істор", StringComparison.Ordinal));
    }

    private static bool IsMemoryIntent(string value)
    {
        return HasActionCue(value)
            && (value.Contains("memory", StringComparison.Ordinal)
                || value.Contains("active memory", StringComparison.Ordinal)
                || value.Contains("пам'ять", StringComparison.Ordinal)
                || value.Contains("памят", StringComparison.Ordinal));
    }

    private static bool IsPromptShowIntent(string value)
    {
        return HasActionCue(value)
            && (value.Contains("system prompt", StringComparison.Ordinal)
                || value.Contains("current prompt", StringComparison.Ordinal)
                || value.Contains("show prompt", StringComparison.Ordinal)
                || value.Contains("системний промпт", StringComparison.Ordinal)
                || value.Contains("покажи промпт", StringComparison.Ordinal));
    }

    private static bool IsPromptResetIntent(string value)
    {
        var hasPrompt = value.Contains("prompt", StringComparison.Ordinal)
            || value.Contains("промпт", StringComparison.Ordinal);

        var hasReset = value.Contains("reset", StringComparison.Ordinal)
            || value.Contains("default", StringComparison.Ordinal)
            || value.Contains("restore", StringComparison.Ordinal)
            || value.Contains("скинь", StringComparison.Ordinal)
            || value.Contains("віднов", StringComparison.Ordinal)
            || value.Contains("дефолт", StringComparison.Ordinal);

        return hasPrompt && hasReset;
    }

    private static bool IsSyncStatusIntent(string value)
    {
        var hasSync = value.Contains("sync", StringComparison.Ordinal)
            || value.Contains("queue", StringComparison.Ordinal)
            || value.Contains("черга", StringComparison.Ordinal)
            || value.Contains("синх", StringComparison.Ordinal);

        var asksStatus = value.Contains("status", StringComparison.Ordinal)
            || value.Contains("pending", StringComparison.Ordinal)
            || value.Contains("скільки", StringComparison.Ordinal)
            || value.Contains("стан", StringComparison.Ordinal)
            || value.Contains("show", StringComparison.Ordinal)
            || value.Contains("покажи", StringComparison.Ordinal);

        return hasSync && asksStatus;
    }

    private static bool TryParseSyncRunIntent(string value, out ConversationCommandIntent intent)
    {
        var hasSync = value.Contains("sync", StringComparison.Ordinal)
            || value.Contains("синх", StringComparison.Ordinal);

        var hasRun = value.Contains("run", StringComparison.Ordinal)
            || value.Contains("process", StringComparison.Ordinal)
            || value.Contains("retry", StringComparison.Ordinal)
            || value.Contains("start", StringComparison.Ordinal)
            || value.Contains("запусти", StringComparison.Ordinal)
            || value.Contains("оброб", StringComparison.Ordinal)
            || value.Contains("повтори", StringComparison.Ordinal);

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

    private static bool IsResetIntent(string value)
    {
        var hasResetVerb = value.Contains("reset", StringComparison.Ordinal)
            || value.Contains("clear", StringComparison.Ordinal)
            || value.Contains("new session", StringComparison.Ordinal)
            || value.Contains("скинь", StringComparison.Ordinal)
            || value.Contains("очист", StringComparison.Ordinal)
            || value.Contains("нову сесію", StringComparison.Ordinal)
            || value.Contains("нова сесія", StringComparison.Ordinal);

        var hasTarget = value.Contains("conversation", StringComparison.Ordinal)
            || value.Contains("session", StringComparison.Ordinal)
            || value.Contains("context", StringComparison.Ordinal)
            || value.Contains("контекст", StringComparison.Ordinal)
            || value.Contains("сесі", StringComparison.Ordinal)
            || value.Contains("розмов", StringComparison.Ordinal);

        return hasResetVerb && hasTarget;
    }

    private static bool HasActionCue(string value)
    {
        return value.Contains("show", StringComparison.Ordinal)
            || value.Contains("get", StringComparison.Ordinal)
            || value.Contains("display", StringComparison.Ordinal)
            || value.Contains("what is", StringComparison.Ordinal)
            || value.Contains("покажи", StringComparison.Ordinal)
            || value.Contains("виведи", StringComparison.Ordinal)
            || value.Contains("дай", StringComparison.Ordinal);
    }

    private static string Normalize(string input)
    {
        return Regex.Replace(input?.Trim().ToLowerInvariant() ?? string.Empty, "\\s+", " ");
    }
}
