namespace LagerthaAssistant.Application.Services;

using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.AI;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Memory;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Models.AI;
using LagerthaAssistant.Domain.AI;
using LagerthaAssistant.Domain.Abstractions;
using LagerthaAssistant.Domain.Constants;
using LagerthaAssistant.Domain.Entities;

public sealed class AssistantSessionService : IAssistantSessionService
{
    private const int PromptHistoryTakeForGeneration = 5;

    private static readonly Regex MeaningLineRegex = new("^\\((?<pos>[a-z]+)\\)\\s+.+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly char[] IrregularFormSeparators = ['-', ',', '/', '='];
    private static readonly HashSet<string> PhrasalParticles = new(StringComparer.OrdinalIgnoreCase)
    {
        "back", "up", "down", "out", "off", "on", "in", "over", "away", "through", "around", "about", "along", "across", "apart", "by", "into", "onto", "under"
    };

    private static readonly HashSet<string> PhrasalMiddlePronouns = new(StringComparer.OrdinalIgnoreCase)
    {
        "me", "you", "him", "her", "it", "us", "them"
    };

    private static readonly HashSet<string> NonVerbStarters = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "to", "in", "on", "at", "for", "with", "of", "by", "from", "as", "if", "when", "while", "because", "although", "that", "this", "these", "those", "there", "here", "it", "he", "she", "they", "we", "you", "i", "my", "your", "our", "their"
    };

    private readonly IAiChatClient _aiChatClient;
    private readonly IConversationSessionRepository _sessionRepository;
    private readonly IConversationHistoryRepository _historyRepository;
    private readonly IUserMemoryRepository _userMemoryRepository;
    private readonly ISystemPromptRepository _systemPromptRepository;
    private readonly ISystemPromptProposalRepository _systemPromptProposalRepository;
    private readonly IConversationMemoryExtractor _memoryExtractor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AssistantSessionOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<AssistantSessionService> _logger;

    private readonly Conversation _conversation;
    private string _currentSystemPrompt;
    private Guid _sessionKey = Guid.NewGuid();
    private ConversationSession? _session;
    private bool _isInitialized;
    private bool _isSystemPromptLoaded;

    public AssistantSessionService(
        IAiChatClient aiChatClient,
        IConversationSessionRepository sessionRepository,
        IConversationHistoryRepository historyRepository,
        IUserMemoryRepository userMemoryRepository,
        ISystemPromptRepository systemPromptRepository,
        ISystemPromptProposalRepository systemPromptProposalRepository,
        IConversationMemoryExtractor memoryExtractor,
        IUnitOfWork unitOfWork,
        AssistantSessionOptions options,
        IClock clock,
        ILogger<AssistantSessionService> logger)
    {
        _aiChatClient = aiChatClient;
        _sessionRepository = sessionRepository;
        _historyRepository = historyRepository;
        _userMemoryRepository = userMemoryRepository;
        _systemPromptRepository = systemPromptRepository;
        _systemPromptProposalRepository = systemPromptProposalRepository;
        _memoryExtractor = memoryExtractor;
        _unitOfWork = unitOfWork;
        _options = options;
        _clock = clock;
        _logger = logger;
        _currentSystemPrompt = _options.SystemPrompt;
        _conversation = new Conversation(_currentSystemPrompt, _clock);
    }

    public IReadOnlyCollection<ConversationMessage> Messages => _conversation.Messages;

    public async Task<AssistantCompletionResult> AskAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(_options.MaxHistoryMessages, cancellationToken);

        _conversation.AddUserMessage(userMessage, _clock);
        _conversation.TrimHistory(Math.Max(ConversationRules.MinMessagesToKeep, _options.MaxHistoryMessages));

        var contextMessages = await BuildContextMessagesAsync(cancellationToken);
        var completion = await _aiChatClient.CompleteAsync(contextMessages, cancellationToken);
        completion = NormalizePersistentExpressionCompletion(completion);
        completion = NormalizePhrasalVerbCompletion(completion);
        completion = NormalizeIrregularVerbCompletion(completion);
        completion = NormalizeRegularVerbCompletion(completion);

        _conversation.AddAssistantMessage(completion.Content, _clock);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var session = await GetOrCreateSessionAsync(cancellationToken);

            await _historyRepository.AddAsync(
                ConversationHistoryEntry.Create(session, MessageRole.User, userMessage.Trim(), _clock.UtcNow),
                cancellationToken);

            await _historyRepository.AddAsync(
                ConversationHistoryEntry.Create(session, MessageRole.Assistant, completion.Content, _clock.UtcNow),
                cancellationToken);

            await UpsertMemoryAsync(userMessage, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation(
                "Assistant reply generated. SessionKey: {SessionKey}; Model: {Model}; Tokens: {Tokens}",
                _sessionKey,
                completion.Model,
                completion.Usage?.TotalTokens);

            return completion;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.LogError(ex, "Failed to persist conversation history/memory for SessionKey {SessionKey}", _sessionKey);
            throw;
        }
    }

    public async Task<IReadOnlyCollection<ConversationMessage>> GetRecentHistoryAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        var normalizedTake = Math.Max(ConversationRules.MinMessagesToKeep, take);
        await EnsureInitializedAsync(normalizedTake, cancellationToken);

        if (_session is null)
        {
            return _conversation.Messages;
        }

        var entries = await _historyRepository.GetRecentBySessionIdAsync(_session.Id, normalizedTake, cancellationToken);

        return entries
            .Select(x => ConversationMessage.Create(x.Role, x.Content, x.SentAtUtc))
            .ToList();
    }

    public async Task<IReadOnlyCollection<UserMemoryEntry>> GetActiveMemoryAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        var normalizedTake = Math.Max(1, take);
        var memories = await _userMemoryRepository.GetActiveAsync(normalizedTake, cancellationToken);
        return memories;
    }

    public async Task<string> GetSystemPromptAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSystemPromptLoadedAsync(cancellationToken);
        return _currentSystemPrompt;
    }

    public async Task<IReadOnlyCollection<SystemPromptEntry>> GetSystemPromptHistoryAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        var normalizedTake = Math.Max(1, take);
        await EnsureSystemPromptLoadedAsync(cancellationToken);

        return await _systemPromptRepository.GetRecentAsync(normalizedTake, cancellationToken);
    }

    public async Task<IReadOnlyCollection<SystemPromptProposal>> GetSystemPromptProposalsAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        var normalizedTake = Math.Max(1, take);
        return await _systemPromptProposalRepository.GetRecentAsync(normalizedTake, cancellationToken);
    }

    public async Task<SystemPromptProposal> CreateSystemPromptProposalAsync(
        string prompt,
        string reason,
        double confidence,
        string source = "manual",
        CancellationToken cancellationToken = default)
    {
        var normalizedPrompt = prompt?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPrompt))
        {
            throw new ArgumentException("Proposed prompt cannot be empty.", nameof(prompt));
        }

        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? "No reason provided."
            : reason.Trim();

        var boundedConfidence = Math.Clamp(confidence, 0.0, 1.0);

        var proposal = new SystemPromptProposal
        {
            ProposedPrompt = normalizedPrompt,
            Reason = normalizedReason,
            Confidence = boundedConfidence,
            Source = string.IsNullOrWhiteSpace(source) ? "manual" : source.Trim(),
            Status = SystemPromptProposalStatuses.Pending,
            CreatedAtUtc = _clock.UtcNow
        };

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await _systemPromptProposalRepository.AddAsync(proposal, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return proposal;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<SystemPromptProposal> GenerateSystemPromptProposalAsync(
        string goal,
        CancellationToken cancellationToken = default)
    {
        await EnsureSystemPromptLoadedAsync(cancellationToken);

        var normalizedGoal = goal?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedGoal))
        {
            throw new ArgumentException("Goal cannot be empty.", nameof(goal));
        }

        var recentHistory = await _historyRepository.GetRecentBySessionIdAsync(_session?.Id ?? 0, PromptHistoryTakeForGeneration, cancellationToken);
        var historySummary = recentHistory.Count == 0
            ? "No recent chat history."
            : string.Join(Environment.NewLine, recentHistory.Select(x => $"{x.Role}: {x.Content}"));

        var suggestionMessages = new List<ConversationMessage>
        {
            ConversationMessage.Create(
                MessageRole.System,
                "You design concise system prompts for an AI coding assistant. Return ONLY the improved system prompt text with no explanations.",
                _clock.UtcNow),
            ConversationMessage.Create(
                MessageRole.User,
                $"Current system prompt:\n{_currentSystemPrompt}\n\nImprovement goal:\n{normalizedGoal}\n\nRecent dialogue context:\n{historySummary}",
                _clock.UtcNow)
        };

        var completion = await _aiChatClient.CompleteAsync(suggestionMessages, cancellationToken);
        var proposedPrompt = completion.Content.Trim();

        return await CreateSystemPromptProposalAsync(
            proposedPrompt,
            $"AI-generated from goal: {normalizedGoal}",
            0.7,
            "assistant",
            cancellationToken);
    }

    public async Task<string> ApplySystemPromptProposalAsync(
        int proposalId,
        CancellationToken cancellationToken = default)
    {
        var proposal = await _systemPromptProposalRepository.GetByIdAsync(proposalId, cancellationToken)
            ?? throw new InvalidOperationException($"System prompt proposal with id {proposalId} was not found.");

        if (!proposal.Status.Equals(SystemPromptProposalStatuses.Pending, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Proposal {proposalId} is not pending.");
        }

        var appliedPrompt = await SetSystemPromptAsync(proposal.ProposedPrompt, proposal.Source, cancellationToken);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            proposal.Status = SystemPromptProposalStatuses.Applied;
            proposal.ReviewedAtUtc = _clock.UtcNow;

            var activePrompt = await _systemPromptRepository.GetActiveAsync(cancellationToken);
            proposal.AppliedSystemPromptEntryId = activePrompt?.Id;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return appliedPrompt;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task RejectSystemPromptProposalAsync(
        int proposalId,
        CancellationToken cancellationToken = default)
    {
        var proposal = await _systemPromptProposalRepository.GetByIdAsync(proposalId, cancellationToken)
            ?? throw new InvalidOperationException($"System prompt proposal with id {proposalId} was not found.");

        if (!proposal.Status.Equals(SystemPromptProposalStatuses.Pending, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Proposal {proposalId} is not pending.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            proposal.Status = SystemPromptProposalStatuses.Rejected;
            proposal.ReviewedAtUtc = _clock.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<string> SetSystemPromptAsync(
        string prompt,
        string source = "manual",
        CancellationToken cancellationToken = default)
    {
        var normalizedPrompt = prompt?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPrompt))
        {
            throw new ArgumentException("System prompt cannot be empty.", nameof(prompt));
        }

        await EnsureSystemPromptLoadedAsync(cancellationToken);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var active = await _systemPromptRepository.GetActiveAsync(cancellationToken);
            if (active is not null)
            {
                active.IsActive = false;
            }

            var nextVersion = await _systemPromptRepository.GetLatestVersionAsync(cancellationToken) + 1;
            var entry = CreateSystemPromptEntry(normalizedPrompt, nextVersion, source, _clock.UtcNow);
            await _systemPromptRepository.AddAsync(entry, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _currentSystemPrompt = entry.PromptText;
            ResetConversationState();
            _isSystemPromptLoaded = true;

            _logger.LogInformation("System prompt updated. Version: {Version}; Source: {Source}", entry.Version, entry.Source);

            return _currentSystemPrompt;
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    public void Reset()
    {
        ResetConversationState();
    }

    private static AssistantCompletionResult NormalizeIrregularVerbCompletion(AssistantCompletionResult completion)
    {
        var normalizedContent = NormalizeIrregularVerbReply(completion.Content);
        if (string.Equals(normalizedContent, completion.Content, StringComparison.Ordinal))
        {
            return completion;
        }

        return completion with { Content = normalizedContent };
    }

    private static AssistantCompletionResult NormalizeRegularVerbCompletion(AssistantCompletionResult completion)
    {
        var normalizedContent = NormalizeRegularVerbReply(completion.Content);
        if (string.Equals(normalizedContent, completion.Content, StringComparison.Ordinal))
        {
            return completion;
        }

        return completion with { Content = normalizedContent };
    }

    private static AssistantCompletionResult NormalizePersistentExpressionCompletion(AssistantCompletionResult completion)
    {
        var normalizedContent = NormalizePersistentExpressionReply(completion.Content);
        if (string.Equals(normalizedContent, completion.Content, StringComparison.Ordinal))
        {
            return completion;
        }

        return completion with { Content = normalizedContent };
    }

    private static AssistantCompletionResult NormalizePhrasalVerbCompletion(AssistantCompletionResult completion)
    {
        var normalizedContent = NormalizePhrasalVerbReply(completion.Content);
        if (string.Equals(normalizedContent, completion.Content, StringComparison.Ordinal))
        {
            return completion;
        }

        return completion with { Content = normalizedContent };
    }

    private static string NormalizePersistentExpressionReply(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        var lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count == 0)
        {
            return content;
        }

        var header = lines[0].Trim();
        if (!LooksLikePersistentExpression(header))
        {
            return content;
        }

        var meanings = new List<string>();
        var examplesSection = false;

        foreach (var rawLine in lines.Skip(1))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var match = MeaningLineRegex.Match(line);
            if (!examplesSection && match.Success)
            {
                var closeIndex = line.IndexOf(')');
                var meaningBody = closeIndex >= 0
                    ? line[(closeIndex + 1)..].TrimStart()
                    : line.Trim('(', ')', ' ');

                meanings.Add($"(pe) {meaningBody}");
                continue;
            }

            examplesSection = true;
        }

        if (meanings.Count == 0)
        {
            return content;
        }

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            new[]
            {
                CapitalizeFirstLetter(header),
                string.Join(Environment.NewLine + Environment.NewLine, meanings)
            });
    }
    private static string NormalizePhrasalVerbReply(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        var lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count == 0)
        {
            return content;
        }

        var header = lines[0].Trim().ToLowerInvariant();
        if (!LooksLikePhrasalVerb(header))
        {
            return content;
        }

        var meanings = new List<string>();
        var examples = new List<string>();
        var examplesSection = false;

        foreach (var rawLine in lines.Skip(1))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var match = MeaningLineRegex.Match(line);
            if (!examplesSection && match.Success)
            {
                if (line.StartsWith("(v)", StringComparison.OrdinalIgnoreCase))
                {
                    meanings.Add("(pv)" + line[3..]);
                }
                else
                {
                    meanings.Add(line);
                }

                continue;
            }

            examplesSection = true;
            examples.Add(line);
        }

        if (meanings.Count == 0 || examples.Count == 0)
        {
            return content;
        }

        if (!meanings.Any(line => line.StartsWith("(pv)", StringComparison.OrdinalIgnoreCase)))
        {
            return content;
        }

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            new[]
            {
                header,
                string.Join(Environment.NewLine + Environment.NewLine, meanings),
                string.Join(Environment.NewLine + Environment.NewLine, examples)
            });
    }

    private static bool LooksLikePhrasalVerb(string header)
    {
        if (string.IsNullOrWhiteSpace(header)
            || header.Contains(" - ", StringComparison.Ordinal)
            || header.Contains(',', StringComparison.Ordinal)
            || header.Contains('=', StringComparison.Ordinal))
        {
            return false;
        }

        var tokens = header
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length < 2 || tokens.Length > 3)
        {
            return false;
        }

        if (!IsLikelyVerbStarter(tokens[0]))
        {
            return false;
        }

        if (tokens.Length == 2)
        {
            return PhrasalParticles.Contains(tokens[1]);
        }

        return PhrasalMiddlePronouns.Contains(tokens[1])
            && PhrasalParticles.Contains(tokens[2]);
    }

    private static bool LooksLikePersistentExpression(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        var normalized = header.Trim().ToLowerInvariant();
        if (SplitIrregularForms(normalized).Count >= 3)
        {
            return false;
        }

        if (LooksLikePhrasalVerb(normalized))
        {
            return false;
        }

        var hasSpaces = normalized.Contains(' ', StringComparison.Ordinal);
        var hasExpressionPunctuation = normalized.Any(IsExpressionPunctuation);

        return hasSpaces || hasExpressionPunctuation;
    }

    private static bool IsLikelyVerbStarter(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        if (NonVerbStarters.Contains(token))
        {
            return false;
        }

        return token.All(char.IsLetter);
    }

    private static bool IsExpressionPunctuation(char value)
    {
        return value is '.' or '!' or '?' or ':' or ';' or ',';
    }

    private static string CapitalizeFirstLetter(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var trimmed = value.Trim();
        var first = trimmed[0];
        if (!char.IsLetter(first))
        {
            return trimmed;
        }

        var capitalizedFirst = char.ToUpperInvariant(first);
        return trimmed.Length == 1
            ? capitalizedFirst.ToString()
            : capitalizedFirst + trimmed[1..];
    }

    private static string NormalizeIrregularVerbReply(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        var lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count == 0)
        {
            return content;
        }

        var header = lines[0].Trim().ToLowerInvariant();
        var forms = SplitIrregularForms(header);
        if (forms.Count < 3)
        {
            return content;
        }

        var meanings = new List<string>();
        var examples = new List<string>();
        var examplesSection = false;

        foreach (var rawLine in lines.Skip(1))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var match = MeaningLineRegex.Match(line);
            if (!examplesSection && match.Success)
            {
                if (line.StartsWith("(v)", StringComparison.OrdinalIgnoreCase))
                {
                    meanings.Add("(iv)" + line[3..]);
                }
                else
                {
                    meanings.Add(line);
                }

                continue;
            }

            examplesSection = true;
            examples.Add(line);
        }

        if (meanings.Count == 0 || examples.Count == 0)
        {
            return content;
        }

        if (!meanings.Any(line => line.StartsWith("(iv)", StringComparison.OrdinalIgnoreCase)))
        {
            if (meanings[0].StartsWith("(", StringComparison.Ordinal))
            {
                var close = meanings[0].IndexOf(')');
                meanings[0] = close >= 0
                    ? "(iv) " + meanings[0][(close + 1)..].TrimStart()
                    : "(iv) " + meanings[0].TrimStart('(', ')', ' ');
            }
            else
            {
                meanings[0] = "(iv) " + meanings[0];
            }
        }

        EnsureIrregularExamples(forms, examples);

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            new[]
            {
                header,
                string.Join(Environment.NewLine + Environment.NewLine, meanings),
                string.Join(Environment.NewLine + Environment.NewLine, examples)
            });
    }

    private static string NormalizeRegularVerbReply(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        var lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (lines.Count == 0)
        {
            return content;
        }

        var header = lines[0].Trim().ToLowerInvariant();
        var forms = SplitIrregularForms(header);
        if (forms.Count >= 3)
        {
            return content;
        }

        var meanings = new List<string>();
        var examples = new List<string>();
        var examplesSection = false;
        var hasChanges = false;

        foreach (var rawLine in lines.Skip(1))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var match = MeaningLineRegex.Match(line);
            if (!examplesSection && match.Success)
            {
                if (line.StartsWith("(iv)", StringComparison.OrdinalIgnoreCase))
                {
                    meanings.Add("(v)" + line[4..]);
                    hasChanges = true;
                }
                else
                {
                    meanings.Add(line);
                }

                continue;
            }

            examplesSection = true;
            examples.Add(line);
        }

        if (!hasChanges || meanings.Count == 0)
        {
            return content;
        }

        var sections = new List<string>
        {
            header,
            string.Join(Environment.NewLine + Environment.NewLine, meanings)
        };

        if (examples.Count > 0)
        {
            sections.Add(string.Join(Environment.NewLine + Environment.NewLine, examples));
        }

        return string.Join(Environment.NewLine + Environment.NewLine, sections);
    }
    private static void EnsureIrregularExamples(IReadOnlyList<string> forms, List<string> examples)
    {
        if (examples.Count >= 3)
        {
            return;
        }

        var baseForm = forms[0];
        var pastForm = forms[1];
        var pastParticiple = forms[^1];

        var fallbackExamples = new[]
        {
            $"We {baseForm} this improvement in our next sprint.",
            $"Last month the team {pastForm} a large refactoring task.",
            $"The migration has been {pastParticiple} by the platform engineers."
        };

        while (examples.Count < 3)
        {
            examples.Add(fallbackExamples[examples.Count]);
        }
    }

    private static IReadOnlyList<string> SplitIrregularForms(string header)
    {
        var shouldSplit = header.Contains(" - ", StringComparison.Ordinal)
            || header.Contains(',', StringComparison.Ordinal)
            || header.Contains('=', StringComparison.Ordinal)
            || header.Count(ch => ch == '-') >= 2;

        if (!shouldSplit)
        {
            return [];
        }

        return header
            .Split(IrregularFormSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private async Task EnsureInitializedAsync(int historyBootstrapTake, CancellationToken cancellationToken)
    {
        await EnsureSystemPromptLoadedAsync(cancellationToken);

        if (_isInitialized)
        {
            return;
        }

        _session = await _sessionRepository.GetLatestAsync(cancellationToken);
        if (_session is null)
        {
            _isInitialized = true;
            return;
        }

        _sessionKey = _session.SessionKey;

        var history = await _historyRepository.GetRecentBySessionIdAsync(_session.Id, historyBootstrapTake, cancellationToken);
        foreach (var item in history)
        {
            _conversation.AddHistoricalMessage(item.Role, item.Content, item.SentAtUtc);
        }

        _conversation.TrimHistory(Math.Max(ConversationRules.MinMessagesToKeep, _options.MaxHistoryMessages));

        _isInitialized = true;
    }

    private async Task EnsureSystemPromptLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isSystemPromptLoaded)
        {
            return;
        }

        var active = await _systemPromptRepository.GetActiveAsync(cancellationToken);
        if (active is null)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var initialEntry = CreateSystemPromptEntry(_options.SystemPrompt, 1, "seed", _clock.UtcNow);
                await _systemPromptRepository.AddAsync(initialEntry, cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _currentSystemPrompt = initialEntry.PromptText;
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
        else
        {
            _currentSystemPrompt = active.PromptText;
        }

        _conversation.Reset(_currentSystemPrompt, _clock);
        _isSystemPromptLoaded = true;
    }

    private static SystemPromptEntry CreateSystemPromptEntry(string prompt, int version, string source, DateTimeOffset createdAtUtc)
    {
        return new SystemPromptEntry
        {
            PromptText = prompt,
            Version = version,
            IsActive = true,
            Source = string.IsNullOrWhiteSpace(source) ? "manual" : source.Trim(),
            CreatedAtUtc = createdAtUtc
        };
    }

    private void ResetConversationState()
    {
        _conversation.Reset(_currentSystemPrompt, _clock);
        _sessionKey = Guid.NewGuid();
        _session = null;
        _isInitialized = true;
    }

    private async Task<ConversationSession> GetOrCreateSessionAsync(CancellationToken cancellationToken)
    {
        if (_session is not null)
        {
            return _session;
        }

        _session = await _sessionRepository.GetBySessionKeyAsync(_sessionKey, cancellationToken);
        if (_session is not null)
        {
            return _session;
        }

        _session = ConversationSession.Create(_sessionKey, $"Session {_clock.UtcNow:yyyy-MM-dd HH:mm:ss}");
        await _sessionRepository.AddAsync(_session, cancellationToken);

        return _session;
    }

    private async Task UpsertMemoryAsync(string userMessage, CancellationToken cancellationToken)
    {
        var facts = _memoryExtractor.ExtractFromUserMessage(userMessage);

        foreach (var fact in facts)
        {
            var existing = await _userMemoryRepository.GetByKeyAsync(fact.Key, cancellationToken);

            if (existing is null)
            {
                await _userMemoryRepository.AddAsync(new UserMemoryEntry
                {
                    Key = fact.Key,
                    Value = fact.Value,
                    Confidence = fact.Confidence,
                    IsActive = true,
                    LastSeenAtUtc = _clock.UtcNow
                }, cancellationToken);

                continue;
            }

            existing.Value = fact.Value;
            existing.Confidence = fact.Confidence;
            existing.IsActive = true;
            existing.LastSeenAtUtc = _clock.UtcNow;
        }
    }

    private async Task<IReadOnlyCollection<ConversationMessage>> BuildContextMessagesAsync(CancellationToken cancellationToken)
    {
        var memories = await _userMemoryRepository.GetActiveAsync(MemoryContextConstants.MemoryContextTake, cancellationToken);
        if (memories.Count == 0)
        {
            return _conversation.Messages;
        }

        var memoryBlock = string.Join(Environment.NewLine, memories.Select(x => $"- {x.Key}: {x.Value}"));
        var memoryContextMessage = ConversationMessage.Create(
            MessageRole.System,
            $"{MemoryContextConstants.SystemContextPrefix}{Environment.NewLine}{memoryBlock}",
            _clock.UtcNow);

        var messages = _conversation.Messages.ToList();
        messages.Insert(1, memoryContextMessage);

        return messages;
    }
}

