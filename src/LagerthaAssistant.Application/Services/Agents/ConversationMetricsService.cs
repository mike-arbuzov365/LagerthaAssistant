namespace LagerthaAssistant.Application.Services.Agents;

using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Domain.Abstractions;
using Microsoft.Extensions.Logging;

public sealed class ConversationMetricsService : IConversationMetricsService
{
    private readonly IConversationIntentMetricRepository _metricsRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<ConversationMetricsService> _logger;

    public ConversationMetricsService(
        IConversationIntentMetricRepository metricsRepository,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<ConversationMetricsService> logger)
    {
        _metricsRepository = metricsRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task TrackAsync(
        string channel,
        ConversationAgentResult result,
        CancellationToken cancellationToken = default)
    {
        var normalizedChannel = NormalizeChannel(channel);
        var normalizedAgent = NormalizeText(result.AgentName, "unknown-agent");
        var normalizedIntent = NormalizeText(result.Intent, "unknown-intent");
        var now = _clock.UtcNow;

        await _metricsRepository.IncrementAsync(
            now.UtcDateTime.Date,
            normalizedChannel,
            normalizedAgent,
            normalizedIntent,
            result.IsBatch,
            result.Items.Count,
            now,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Conversation metric tracked. Channel={Channel}, Agent={Agent}, Intent={Intent}, IsBatch={IsBatch}, Items={ItemsCount}",
            normalizedChannel,
            normalizedAgent,
            normalizedIntent,
            result.IsBatch,
            result.Items.Count);
    }

    public Task<IReadOnlyList<ConversationIntentMetricSummary>> GetTopIntentsAsync(
        int days,
        int take,
        string? channel = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedDays = Math.Clamp(days, 1, 90);
        var normalizedTake = Math.Clamp(take, 1, 200);
        var fromDateUtc = _clock.UtcNow.UtcDateTime.Date.AddDays(-(normalizedDays - 1));
        var normalizedChannel = string.IsNullOrWhiteSpace(channel)
            ? null
            : NormalizeChannel(channel);

        return _metricsRepository.GetTopAsync(fromDateUtc, normalizedChannel, normalizedTake, cancellationToken);
    }

    private static string NormalizeChannel(string? channel)
    {
        return NormalizeText(channel, "unknown");
    }

    private static string NormalizeText(string? value, string fallback)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized)
            ? fallback
            : normalized;
    }
}
