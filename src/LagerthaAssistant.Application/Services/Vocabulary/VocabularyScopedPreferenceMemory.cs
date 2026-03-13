namespace LagerthaAssistant.Application.Services.Vocabulary;

using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Domain.Entities;

internal static class VocabularyScopedPreferenceMemory
{
    public static async Task<UserMemoryEntry?> GetScopedOrLegacyEntryAsync(
        IUserMemoryRepository userMemoryRepository,
        string key,
        ConversationScope scope,
        CancellationToken cancellationToken)
    {
        var scoped = await userMemoryRepository.GetByKeyAsync(
            key,
            scope.Channel,
            scope.UserId,
            cancellationToken);

        if (scoped is not null)
        {
            return scoped;
        }

        if (scope.Channel.Equals(ConversationScope.DefaultChannel, StringComparison.Ordinal)
            && scope.UserId.Equals(ConversationScope.DefaultUserId, StringComparison.Ordinal))
        {
            return null;
        }

        return await userMemoryRepository.GetByKeyAsync(key, cancellationToken);
    }

    public static async Task UpsertScopedEntryAsync(
        IUserMemoryRepository userMemoryRepository,
        IUnitOfWork unitOfWork,
        string key,
        string value,
        ConversationScope scope,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var entry = await userMemoryRepository.GetByKeyAsync(
            key,
            scope.Channel,
            scope.UserId,
            cancellationToken);

        if (entry is null)
        {
            await userMemoryRepository.AddAsync(new UserMemoryEntry
            {
                Key = key,
                Value = value,
                Confidence = 1.0,
                IsActive = false,
                LastSeenAtUtc = now,
                Channel = scope.Channel,
                UserId = scope.UserId
            }, cancellationToken);
        }
        else
        {
            entry.Value = value;
            entry.Confidence = 1.0;
            entry.IsActive = false;
            entry.LastSeenAtUtc = now;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
