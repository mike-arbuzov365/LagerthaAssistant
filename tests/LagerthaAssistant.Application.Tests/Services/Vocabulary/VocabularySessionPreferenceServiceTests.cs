namespace LagerthaAssistant.Application.Tests.Services.Vocabulary;

using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using LagerthaAssistant.Application.Services.Vocabulary;
using Xunit;

public sealed class VocabularySessionPreferenceServiceTests
{
    [Fact]
    public async Task GetAsync_ShouldReturnBothModes_AndUpdateCurrentStorageMode()
    {
        var saveModeService = new FakeSaveModePreferenceService { CurrentMode = VocabularySaveMode.Auto };
        var storagePreferenceService = new FakeStoragePreferenceService { CurrentMode = VocabularyStorageMode.Graph };
        var storageModeProvider = new FakeStorageModeProvider();

        var sut = new VocabularySessionPreferenceService(
            saveModeService,
            storagePreferenceService,
            storageModeProvider);

        var scope = ConversationScope.Create("api", "mike", "chat-42");
        var result = await sut.GetAsync(scope, CancellationToken.None);

        Assert.Equal(VocabularySaveMode.Auto, result.SaveMode);
        Assert.Equal(VocabularyStorageMode.Graph, result.StorageMode);
        Assert.Equal(VocabularyStorageMode.Graph, storageModeProvider.CurrentMode);
        Assert.Equal(["ask", "auto", "off"], sut.SupportedSaveModes);
        Assert.Equal(["local", "graph"], sut.SupportedStorageModes);
    }

    [Fact]
    public void SupportedStorageModes_ShouldMirrorStoragePreferenceServiceModes()
    {
        var saveModeService = new FakeSaveModePreferenceService();
        var storagePreferenceService = new FakeStoragePreferenceService
        {
            SupportedModes = ["local", "graph", "hybrid"]
        };
        var storageModeProvider = new FakeStorageModeProvider();

        var sut = new VocabularySessionPreferenceService(
            saveModeService,
            storagePreferenceService,
            storageModeProvider);

        Assert.Equal(["local", "graph", "hybrid"], sut.SupportedStorageModes);
    }

    [Fact]
    public async Task SetAsync_ShouldPersistOnlyProvidedValues_AndReturnEffectiveSession()
    {
        var saveModeService = new FakeSaveModePreferenceService { CurrentMode = VocabularySaveMode.Ask };
        var storagePreferenceService = new FakeStoragePreferenceService { CurrentMode = VocabularyStorageMode.Local };
        var storageModeProvider = new FakeStorageModeProvider();

        var sut = new VocabularySessionPreferenceService(
            saveModeService,
            storagePreferenceService,
            storageModeProvider);

        var scope = ConversationScope.Create("telegram", "mike", "chat-42");
        var result = await sut.SetAsync(
            scope,
            saveMode: VocabularySaveMode.Off,
            storageMode: null,
            cancellationToken: CancellationToken.None);

        Assert.Equal(VocabularySaveMode.Off, result.SaveMode);
        Assert.Equal(VocabularyStorageMode.Local, result.StorageMode);
        Assert.Equal(scope, saveModeService.LastSetScope);
        Assert.Null(storagePreferenceService.LastSetScope);
        Assert.Equal(VocabularyStorageMode.Local, storageModeProvider.CurrentMode);
    }

    [Fact]
    public async Task SetAsync_ShouldPersistBothValues_WhenBothProvided()
    {
        var saveModeService = new FakeSaveModePreferenceService { CurrentMode = VocabularySaveMode.Ask };
        var storagePreferenceService = new FakeStoragePreferenceService { CurrentMode = VocabularyStorageMode.Local };
        var storageModeProvider = new FakeStorageModeProvider();

        var sut = new VocabularySessionPreferenceService(
            saveModeService,
            storagePreferenceService,
            storageModeProvider);

        var scope = ConversationScope.Create("telegram", "mike", "chat-42");
        var result = await sut.SetAsync(
            scope,
            saveMode: VocabularySaveMode.Auto,
            storageMode: VocabularyStorageMode.Graph,
            cancellationToken: CancellationToken.None);

        Assert.Equal(VocabularySaveMode.Auto, result.SaveMode);
        Assert.Equal(VocabularyStorageMode.Graph, result.StorageMode);
        Assert.Equal(scope, saveModeService.LastSetScope);
        Assert.Equal(scope, storagePreferenceService.LastSetScope);
        Assert.Equal(VocabularyStorageMode.Graph, storageModeProvider.CurrentMode);
    }

    private sealed class FakeSaveModePreferenceService : IVocabularySaveModePreferenceService
    {
        public IReadOnlyList<string> SupportedModes { get; } = ["ask", "auto", "off"];

        public VocabularySaveMode CurrentMode { get; set; } = VocabularySaveMode.Ask;

        public ConversationScope? LastGetScope { get; private set; }

        public ConversationScope? LastSetScope { get; private set; }

        public bool TryParse(string? value, out VocabularySaveMode mode)
        {
            mode = CurrentMode;
            return true;
        }

        public string ToText(VocabularySaveMode mode) => mode.ToString().ToLowerInvariant();

        public Task<VocabularySaveMode> GetModeAsync(ConversationScope scope, CancellationToken cancellationToken = default)
        {
            LastGetScope = scope;
            return Task.FromResult(CurrentMode);
        }

        public Task<VocabularySaveMode> SetModeAsync(
            ConversationScope scope,
            VocabularySaveMode mode,
            CancellationToken cancellationToken = default)
        {
            LastSetScope = scope;
            CurrentMode = mode;
            return Task.FromResult(mode);
        }
    }

    private sealed class FakeStoragePreferenceService : IVocabularyStoragePreferenceService
    {
        public IReadOnlyList<string> SupportedModes { get; set; } = ["local", "graph"];

        public VocabularyStorageMode CurrentMode { get; set; } = VocabularyStorageMode.Local;

        public ConversationScope? LastGetScope { get; private set; }

        public ConversationScope? LastSetScope { get; private set; }

        public Task<VocabularyStorageMode> GetModeAsync(ConversationScope scope, CancellationToken cancellationToken = default)
        {
            LastGetScope = scope;
            return Task.FromResult(CurrentMode);
        }

        public Task<VocabularyStorageMode> SetModeAsync(
            ConversationScope scope,
            VocabularyStorageMode mode,
            CancellationToken cancellationToken = default)
        {
            LastSetScope = scope;
            CurrentMode = mode;
            return Task.FromResult(mode);
        }
    }

    private sealed class FakeStorageModeProvider : IVocabularyStorageModeProvider
    {
        public VocabularyStorageMode CurrentMode { get; private set; } = VocabularyStorageMode.Local;

        public void SetMode(VocabularyStorageMode mode)
        {
            CurrentMode = mode;
        }

        public bool TryParse(string? value, out VocabularyStorageMode mode)
        {
            mode = VocabularyStorageMode.Local;
            return true;
        }

        public string ToText(VocabularyStorageMode mode) => mode.ToString().ToLowerInvariant();
    }
}
