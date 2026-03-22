namespace LagerthaAssistant.Api.Services;

using System.Collections.Concurrent;
using LagerthaAssistant.Api.Models;
using LagerthaAssistant.Application.Models.Vocabulary;

/// <summary>
/// Singleton store for all pending in-conversation state managed by <see cref="Controllers.TelegramController"/>.
/// Extracting this into a dedicated injectable singleton fixes flaky parallel-test failures caused by
/// the dictionaries previously being static fields on the controller class.
/// </summary>
public sealed class TelegramPendingStateStore
{
    internal ConcurrentDictionary<string, GraphDeviceLoginChallenge> GraphChallenges { get; }
        = new(StringComparer.Ordinal);

    internal ConcurrentDictionary<string, PendingVocabularySaveRequest> VocabularySaves { get; }
        = new(StringComparer.Ordinal);

    internal ConcurrentDictionary<string, PendingVocabularyBatchSaveRequest> VocabularyBatchSaves { get; }
        = new(StringComparer.Ordinal);

    internal ConcurrentDictionary<string, PendingVocabularyUrlSession> VocabularyUrlSessions { get; }
        = new(StringComparer.Ordinal);

    internal ConcurrentDictionary<string, PendingChatActionKind> ChatActions { get; }
        = new(StringComparer.Ordinal);

    internal ConcurrentDictionary<string, PendingMealCreation> MealCreations { get; }
        = new(StringComparer.Ordinal);

    internal ConcurrentDictionary<string, PendingFoodPhotoLog> FoodPhotoLogs { get; }
        = new(StringComparer.Ordinal);

    /// <summary>Safety valve: prevents unbounded memory growth if users abandon flows.</summary>
    internal void CleanupIfOversized(int threshold = 100)
    {
        CleanupDict(ChatActions, threshold);
        CleanupDict(VocabularySaves, threshold);
        CleanupDict(VocabularyBatchSaves, threshold);
        CleanupDict(VocabularyUrlSessions, threshold);
        CleanupDict(MealCreations, threshold);
        CleanupDict(FoodPhotoLogs, threshold);
        // GraphChallenges excluded — they have their own TTL via OAuth flow
    }

    private static void CleanupDict<TKey, TValue>(ConcurrentDictionary<TKey, TValue> dict, int threshold) where TKey : notnull
    {
        if (dict.Count > threshold)
            dict.Clear();
    }
}
