namespace LagerthaAssistant.Infrastructure.Repositories;

using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Domain.Entities;
using LagerthaAssistant.Infrastructure.Constants;
using LagerthaAssistant.Infrastructure.Data;
using LagerthaAssistant.Infrastructure.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class ConversationIntentMetricRepository : IConversationIntentMetricRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<ConversationIntentMetricRepository> _logger;

    public ConversationIntentMetricRepository(
        AppDbContext context,
        ILogger<ConversationIntentMetricRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task IncrementAsync(
        DateTime metricDateUtc,
        string channel,
        string agentName,
        string intent,
        bool isBatch,
        int itemsCount,
        DateTimeOffset lastSeenAtUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Executing {Operation} for conversation metric {Channel}/{Agent}/{Intent} (batch={IsBatch})",
                RepositoryOperations.Add,
                channel,
                agentName,
                intent,
                isBatch);

            var row = await _context.ConversationIntentMetrics
                .FirstOrDefaultAsync(
                    x => x.MetricDateUtc == metricDateUtc
                        && x.Channel == channel
                        && x.AgentName == agentName
                        && x.Intent == intent
                        && x.IsBatch == isBatch,
                    cancellationToken);

            if (row is null)
            {
                _context.ConversationIntentMetrics.Add(new ConversationIntentMetric
                {
                    MetricDateUtc = metricDateUtc,
                    Channel = channel,
                    AgentName = agentName,
                    Intent = intent,
                    IsBatch = isBatch,
                    Count = 1,
                    TotalItems = Math.Max(0, itemsCount),
                    LastSeenAtUtc = lastSeenAtUtc
                });

                return;
            }

            row.Count += 1;
            row.TotalItems += Math.Max(0, itemsCount);
            row.LastSeenAtUtc = lastSeenAtUtc;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error in {Operation} for conversation metric {Channel}/{Agent}/{Intent} (batch={IsBatch})",
                RepositoryOperations.Add,
                channel,
                agentName,
                intent,
                isBatch);

            throw new RepositoryException(
                nameof(ConversationIntentMetricRepository),
                RepositoryOperations.Add,
                "Failed to increment conversation intent metric",
                ex);
        }
    }

    public async Task<IReadOnlyList<ConversationIntentMetricSummary>> GetTopAsync(
        DateTime fromDateUtc,
        string? channel,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return [];
        }

        try
        {
            _logger.LogDebug(
                "Executing {Operation} for conversation metrics. FromDateUtc={FromDateUtc}, Channel={Channel}, Take={Take}",
                RepositoryOperations.GetActive,
                fromDateUtc,
                channel ?? "*",
                take);

            var query = _context.ConversationIntentMetrics
                .AsNoTracking()
                .Where(x => x.MetricDateUtc >= fromDateUtc);

            if (!string.IsNullOrWhiteSpace(channel))
            {
                query = query.Where(x => x.Channel == channel);
            }

            return await query
                .GroupBy(x => new
                {
                    x.Channel,
                    x.AgentName,
                    x.Intent,
                    x.IsBatch
                })
                .Select(group => new ConversationIntentMetricSummary(
                    group.Key.Channel,
                    group.Key.AgentName,
                    group.Key.Intent,
                    group.Key.IsBatch,
                    group.Sum(x => x.Count),
                    group.Sum(x => x.TotalItems),
                    group.Max(x => x.LastSeenAtUtc)))
                .OrderByDescending(x => x.Count)
                .ThenByDescending(x => x.TotalItems)
                .ThenBy(x => x.Intent)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error in {Operation} for conversation metrics. FromDateUtc={FromDateUtc}, Channel={Channel}",
                RepositoryOperations.GetActive,
                fromDateUtc,
                channel ?? "*");

            throw new RepositoryException(
                nameof(ConversationIntentMetricRepository),
                RepositoryOperations.GetActive,
                "Failed to load conversation intent metrics",
                ex);
        }
    }
}
