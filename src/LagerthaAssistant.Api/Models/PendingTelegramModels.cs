namespace LagerthaAssistant.Api.Models;

using LagerthaAssistant.Api.Interfaces;

internal sealed record PendingVocabularySaveRequest(
    string RequestedWord,
    string AssistantReply,
    string TargetDeckFileName,
    string? OverridePartOfSpeech);

internal sealed record PendingVocabularyBatchSaveRequest(
    IReadOnlyList<PendingVocabularySaveRequest> Items);

internal sealed record PendingVocabularyUrlSession(
    PendingVocabularyUrlStage Stage,
    TelegramImportSourceType? SourceType,
    IReadOnlyList<PendingVocabularyUrlCandidate> Candidates)
{
    public static PendingVocabularyUrlSession AwaitingSourceType { get; }
        = new(PendingVocabularyUrlStage.AwaitingSourceType, null, []);

    public static PendingVocabularyUrlSession AwaitingSourceInput(TelegramImportSourceType sourceType)
        => new(PendingVocabularyUrlStage.AwaitingSourceInput, sourceType, []);

    public static PendingVocabularyUrlSession AwaitingSelection(IReadOnlyList<PendingVocabularyUrlCandidate> candidates)
        => new(PendingVocabularyUrlStage.AwaitingSelection, null, candidates);
}

internal sealed record PendingVocabularyUrlCandidate(
    int Number,
    string Word,
    string PartOfSpeech,
    int Frequency);

internal enum PendingVocabularyUrlStage
{
    AwaitingSourceType = 0,
    AwaitingSourceInput = 1,
    AwaitingSelection = 2
}

internal enum PendingChatActionKind
{
    VocabularyAdd = 0,
    VocabularyBatch = 1,
    VocabularyImport = 2,
    MealCreation = 3
}

internal sealed record PendingMealCreation(
    string Name,
    int? CaloriesPerServing,
    decimal? ProteinGrams,
    decimal? CarbsGrams,
    decimal? FatGrams,
    int? PrepTimeMinutes,
    int DefaultServings,
    IReadOnlyList<PendingMealIngredient> Ingredients);

internal sealed record PendingMealIngredient(string Name, string? Quantity);
