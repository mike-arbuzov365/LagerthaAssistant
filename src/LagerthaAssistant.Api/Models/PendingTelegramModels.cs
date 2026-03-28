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
    InventorySearch = 3,
    MealCreation = 4,
    FoodPhotoLog = 5,
    InventoryAdjustQuantity = 6,
    InventorySetMinQuantity = 7,
    InventoryPhotoAwaitingImage = 8,
    InventoryPhotoAwaitingSelection = 9,
    InventoryPhotoAwaitingStoreResolution = 10,
    InventoryPhotoAwaitingUnknownSelection = 11,
    InventoryPhotoAwaitingItemLink = 12
}

internal sealed record PendingFoodPhotoLog(
    string MealName,
    int EstimatedCalories,
    decimal Servings);

internal sealed record PendingInventoryPhotoSession(
    TelegramInventoryPhotoMode Mode,
    IReadOnlyList<PendingInventoryPhotoCandidate> Candidates,
    IReadOnlyList<PendingInventoryPhotoUnknown> Unknown,
    IReadOnlyList<string>? NonProducts = null,
    string? DetectedStoreName = null,
    string? DetectedStoreNameEn = null,
    double? StoreConfidence = null,
    string? ResolvedStoreName = null);

internal sealed record PendingInventoryPhotoCandidate(
    int Number,
    int ItemId,
    string Name,
    decimal Quantity,
    string? Unit,
    double Confidence,
    string? IconEmoji = null,
    string? Category = null,
    decimal? PriceTotal = null,
    decimal? PricePerUnit = null);

internal sealed record PendingInventoryPhotoUnknown(
    int Number,
    string Name,
    string? NameEn,
    decimal Quantity,
    string? Unit,
    double Confidence,
    decimal? PriceTotal = null,
    decimal? PricePerUnit = null,
    bool IsNonProduct = false,
    string? Category = null,
    string? IconEmoji = null);

internal sealed record PendingShoppingDeleteSession(
    IReadOnlyList<PendingShoppingDeleteCandidate> Candidates);

internal sealed record PendingShoppingDeleteCandidate(
    int Number,
    int ItemId,
    string Name,
    string? Quantity,
    string? Store);

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
