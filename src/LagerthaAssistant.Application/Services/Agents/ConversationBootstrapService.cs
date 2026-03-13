namespace LagerthaAssistant.Application.Services.Agents;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;

public sealed class ConversationBootstrapService : IConversationBootstrapService
{
    private readonly IVocabularySessionPreferenceService _sessionPreferenceService;
    private readonly IVocabularySaveModePreferenceService _saveModePreferenceService;
    private readonly IVocabularyStorageModeProvider _storageModeProvider;
    private readonly IGraphAuthService _graphAuthService;

    public ConversationBootstrapService(
        IVocabularySessionPreferenceService sessionPreferenceService,
        IVocabularySaveModePreferenceService saveModePreferenceService,
        IVocabularyStorageModeProvider storageModeProvider,
        IGraphAuthService graphAuthService)
    {
        _sessionPreferenceService = sessionPreferenceService;
        _saveModePreferenceService = saveModePreferenceService;
        _storageModeProvider = storageModeProvider;
        _graphAuthService = graphAuthService;
    }

    public async Task<ConversationBootstrapSnapshot> BuildAsync(
        ConversationScope scope,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionPreferenceService.GetAsync(scope, cancellationToken);
        var graph = await _graphAuthService.GetStatusAsync(cancellationToken);

        var saveMode = _saveModePreferenceService.ToText(session.SaveMode);
        var storageMode = _storageModeProvider.ToText(session.StorageMode);

        var markerOptions = VocabularyPartOfSpeechCatalog.GetOptions()
            .OrderBy(option => option.Number)
            .ToList();

        return new ConversationBootstrapSnapshot(
            scope,
            saveMode,
            _sessionPreferenceService.SupportedSaveModes,
            storageMode,
            _sessionPreferenceService.SupportedStorageModes,
            graph,
            ConversationCommandCatalog.SlashCommandGroups,
            markerOptions);
    }
}
