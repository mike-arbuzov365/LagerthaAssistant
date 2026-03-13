namespace LagerthaAssistant.Application.Services.Vocabulary;

using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;

public sealed class VocabularySessionPreferenceService : IVocabularySessionPreferenceService
{
    private static readonly IReadOnlyList<string> StorageModes = ["local", "graph"];

    private readonly IVocabularySaveModePreferenceService _saveModePreferenceService;
    private readonly IVocabularyStoragePreferenceService _storagePreferenceService;
    private readonly IVocabularyStorageModeProvider _storageModeProvider;

    public VocabularySessionPreferenceService(
        IVocabularySaveModePreferenceService saveModePreferenceService,
        IVocabularyStoragePreferenceService storagePreferenceService,
        IVocabularyStorageModeProvider storageModeProvider)
    {
        _saveModePreferenceService = saveModePreferenceService;
        _storagePreferenceService = storagePreferenceService;
        _storageModeProvider = storageModeProvider;
    }

    public IReadOnlyList<string> SupportedSaveModes => _saveModePreferenceService.SupportedModes;

    public IReadOnlyList<string> SupportedStorageModes => StorageModes;

    public async Task<VocabularySessionPreferences> GetAsync(
        ConversationScope scope,
        CancellationToken cancellationToken = default)
    {
        var saveMode = await _saveModePreferenceService.GetModeAsync(scope, cancellationToken);
        var storageMode = await _storagePreferenceService.GetModeAsync(scope, cancellationToken);
        _storageModeProvider.SetMode(storageMode);

        return new VocabularySessionPreferences(saveMode, storageMode);
    }

    public async Task<VocabularySessionPreferences> SetAsync(
        ConversationScope scope,
        VocabularySaveMode? saveMode = null,
        VocabularyStorageMode? storageMode = null,
        CancellationToken cancellationToken = default)
    {
        VocabularySaveMode effectiveSaveMode;
        if (saveMode.HasValue)
        {
            await _saveModePreferenceService.SetModeAsync(scope, saveMode.Value, cancellationToken);
            effectiveSaveMode = saveMode.Value;
        }
        else
        {
            effectiveSaveMode = await _saveModePreferenceService.GetModeAsync(scope, cancellationToken);
        }

        VocabularyStorageMode effectiveStorageMode;
        if (storageMode.HasValue)
        {
            await _storagePreferenceService.SetModeAsync(scope, storageMode.Value, cancellationToken);
            effectiveStorageMode = storageMode.Value;
        }
        else
        {
            effectiveStorageMode = await _storagePreferenceService.GetModeAsync(scope, cancellationToken);
        }

        _storageModeProvider.SetMode(effectiveStorageMode);
        return new VocabularySessionPreferences(effectiveSaveMode, effectiveStorageMode);
    }
}
