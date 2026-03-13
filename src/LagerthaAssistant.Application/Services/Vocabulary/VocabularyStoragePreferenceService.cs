namespace LagerthaAssistant.Application.Services.Vocabulary;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Repositories;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;

public sealed class VocabularyStoragePreferenceService : IVocabularyStoragePreferenceService
{
    private readonly IUserMemoryRepository _userMemoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVocabularyStorageModeProvider _storageModeProvider;

    public VocabularyStoragePreferenceService(
        IUserMemoryRepository userMemoryRepository,
        IUnitOfWork unitOfWork,
        IVocabularyStorageModeProvider storageModeProvider)
    {
        _userMemoryRepository = userMemoryRepository;
        _unitOfWork = unitOfWork;
        _storageModeProvider = storageModeProvider;

        SupportedModes = Enum.GetValues<VocabularyStorageMode>()
            .Select(_storageModeProvider.ToText)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<string> SupportedModes { get; }

    public async Task<VocabularyStorageMode> GetModeAsync(ConversationScope scope, CancellationToken cancellationToken = default)
    {
        var entry = await VocabularyScopedPreferenceMemory.GetScopedOrLegacyEntryAsync(
            _userMemoryRepository,
            UserPreferenceMemoryKeys.StorageMode,
            scope,
            cancellationToken);

        if (entry is not null && _storageModeProvider.TryParse(entry.Value, out var parsedMode))
        {
            return parsedMode;
        }

        return _storageModeProvider.CurrentMode;
    }

    public async Task<VocabularyStorageMode> SetModeAsync(
        ConversationScope scope,
        VocabularyStorageMode mode,
        CancellationToken cancellationToken = default)
    {
        var modeValue = _storageModeProvider.ToText(mode);
        await VocabularyScopedPreferenceMemory.UpsertScopedEntryAsync(
            _userMemoryRepository,
            _unitOfWork,
            UserPreferenceMemoryKeys.StorageMode,
            modeValue,
            scope,
            DateTimeOffset.UtcNow,
            cancellationToken);

        return mode;
    }
}
