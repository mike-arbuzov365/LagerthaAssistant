namespace LagerthaAssistant.Application.Services;

using Microsoft.Extensions.Logging;
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
    private readonly IAiChatClient _aiChatClient;
    private readonly IConversationSessionRepository _sessionRepository;
    private readonly IConversationHistoryRepository _historyRepository;
    private readonly IUserMemoryRepository _userMemoryRepository;
    private readonly IConversationMemoryExtractor _memoryExtractor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AssistantSessionOptions _options;
    private readonly IClock _clock;
    private readonly ILogger<AssistantSessionService> _logger;

    private readonly Conversation _conversation;
    private Guid _sessionKey = Guid.NewGuid();
    private ConversationSession? _session;
    private bool _isInitialized;

    public AssistantSessionService(
        IAiChatClient aiChatClient,
        IConversationSessionRepository sessionRepository,
        IConversationHistoryRepository historyRepository,
        IUserMemoryRepository userMemoryRepository,
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
        _memoryExtractor = memoryExtractor;
        _unitOfWork = unitOfWork;
        _options = options;
        _clock = clock;
        _logger = logger;
        _conversation = new Conversation(_options.SystemPrompt, _clock);
    }

    public IReadOnlyCollection<ConversationMessage> Messages => _conversation.Messages;

    public async Task<AssistantCompletionResult> AskAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(_options.MaxHistoryMessages, cancellationToken);

        _conversation.AddUserMessage(userMessage, _clock);
        _conversation.TrimHistory(Math.Max(ConversationRules.MinMessagesToKeep, _options.MaxHistoryMessages));

        var contextMessages = await BuildContextMessagesAsync(cancellationToken);
        var completion = await _aiChatClient.CompleteAsync(contextMessages, cancellationToken);

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

    public void Reset()
    {
        _conversation.Reset(_options.SystemPrompt, _clock);
        _sessionKey = Guid.NewGuid();
        _session = null;
        _isInitialized = true;
    }

    private async Task EnsureInitializedAsync(int historyBootstrapTake, CancellationToken cancellationToken)
    {
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


