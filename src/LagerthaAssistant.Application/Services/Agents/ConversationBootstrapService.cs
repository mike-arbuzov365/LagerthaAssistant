namespace LagerthaAssistant.Application.Services.Agents;

using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces.Agents;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

public sealed class ConversationBootstrapService : IConversationBootstrapService
{
    private static readonly TimeSpan GraphBootstrapTimeout = TimeSpan.FromMilliseconds(250);

    private readonly IVocabularySessionPreferenceService _sessionPreferenceService;
    private readonly IVocabularySaveModePreferenceService _saveModePreferenceService;
    private readonly IVocabularyStorageModeProvider _storageModeProvider;
    private readonly IVocabularyDeckService _deckService;
    private readonly IGraphAuthService _graphAuthService;
    private readonly IConversationCommandCatalogService _commandCatalogService;
    private readonly ILogger<ConversationBootstrapService> _logger;

    public ConversationBootstrapService(
        IVocabularySessionPreferenceService sessionPreferenceService,
        IVocabularySaveModePreferenceService saveModePreferenceService,
        IVocabularyStorageModeProvider storageModeProvider,
        IVocabularyDeckService deckService,
        IGraphAuthService graphAuthService,
        IConversationCommandCatalogService commandCatalogService,
        ILogger<ConversationBootstrapService> logger)
    {
        _sessionPreferenceService = sessionPreferenceService;
        _saveModePreferenceService = saveModePreferenceService;
        _storageModeProvider = storageModeProvider;
        _deckService = deckService;
        _graphAuthService = graphAuthService;
        _commandCatalogService = commandCatalogService;
        _logger = logger;
    }

    public async Task<ConversationBootstrapSnapshot> BuildAsync(
        ConversationScope scope,
        ConversationBootstrapOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= ConversationBootstrapOptions.Default;
        var totalStopwatch = Stopwatch.StartNew();

        // Keep these calls sequential. In production they can share the same scoped persistence
        // graph, and running them concurrently risks EF Core "second operation started" failures.
        var sessionStopwatch = Stopwatch.StartNew();
        var session = await _sessionPreferenceService.GetAsync(scope, cancellationToken);
        sessionStopwatch.Stop();

        var graphStopwatch = Stopwatch.StartNew();
        var graph = await TryGetGraphStatusAsync(cancellationToken);
        graphStopwatch.Stop();

        var saveMode = _saveModePreferenceService.ToText(session.SaveMode);
        var storageMode = _storageModeProvider.ToText(session.StorageMode);

        IReadOnlyList<VocabularyPartOfSpeechOption> markerOptions = options.IncludePartOfSpeechOptions
            ? VocabularyPartOfSpeechCatalog.GetOptions()
                .OrderBy(option => option.Number)
                .ToList()
            : [];

        IReadOnlyList<ConversationCommandCatalogGroup> commandGroups = options.IncludeCommandGroups
            ? _commandCatalogService.GetGroups()
            : [];

        IReadOnlyList<VocabularyDeckFile>? writableDecks = null;
        if (options.IncludeWritableDecks)
        {
            writableDecks = await _deckService.GetWritableDeckFilesAsync(cancellationToken);
        }

        totalStopwatch.Stop();
        _logger.LogInformation(
            "Conversation bootstrap snapshot prepared. Channel={Channel}, UserId={UserId}, ConversationId={ConversationId}, SessionMs={SessionMs}, GraphMs={GraphMs}, TotalMs={TotalMs}, IncludeDecks={IncludeDecks}",
            scope.Channel,
            scope.UserId,
            scope.ConversationId,
            sessionStopwatch.ElapsedMilliseconds,
            graphStopwatch.ElapsedMilliseconds,
            totalStopwatch.ElapsedMilliseconds,
            options.IncludeWritableDecks);

        return new ConversationBootstrapSnapshot(
            scope,
            saveMode,
            _sessionPreferenceService.SupportedSaveModes,
            storageMode,
            _sessionPreferenceService.SupportedStorageModes,
            graph,
            commandGroups,
            markerOptions,
            writableDecks);
    }

    private async Task<GraphAuthStatus> TryGetGraphStatusAsync(CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(GraphBootstrapTimeout);

        try
        {
            return await _graphAuthService.GetStatusAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Conversation bootstrap: Graph status check exceeded {TimeoutMs} ms. Returning lightweight fallback state.",
                GraphBootstrapTimeout.TotalMilliseconds);
            return new GraphAuthStatus(
                IsConfigured: true,
                IsAuthenticated: false,
                Message: "Graph status is loading. Refresh in a moment.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Conversation bootstrap: Graph status check failed. Falling back to degraded integration state.");
            return new GraphAuthStatus(
                IsConfigured: true,
                IsAuthenticated: false,
                Message: "Graph status unavailable. Retry later.");
        }
    }
}
