namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Interfaces.Vocabulary;
using LagerthaAssistant.Application.Models.Agents;
using LagerthaAssistant.Application.Models.Vocabulary;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class PreferencesControllerTests
{
    [Fact]
    public async Task GetSaveMode_ShouldReturnCurrentMode_AndSupportedModes()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var saveModeService = new FakeVocabularySaveModePreferenceService
        {
            CurrentMode = VocabularySaveMode.Auto
        };
        var sut = CreateSut(scopeAccessor, saveModeService);

        var response = await sut.GetSaveMode(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<PreferenceSaveModeResponse>(ok.Value);

        Assert.Equal("auto", payload.Mode);
        Assert.Equal(["ask", "auto", "off"], payload.AvailableModes);
        Assert.Equal("api", scopeAccessor.Current.Channel);
        Assert.Equal("anonymous", scopeAccessor.Current.UserId);
    }

    [Fact]
    public async Task SetSaveMode_ShouldReturnBadRequest_WhenModeMissing()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var saveModeService = new FakeVocabularySaveModePreferenceService();
        var sut = CreateSut(scopeAccessor, saveModeService);

        var response = await sut.SetSaveMode(new PreferenceSetSaveModeRequest("   "), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Null(saveModeService.LastSetScope);
    }

    [Fact]
    public async Task SetSaveMode_ShouldReturnBadRequest_WhenModeUnsupported()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var saveModeService = new FakeVocabularySaveModePreferenceService();
        var sut = CreateSut(scopeAccessor, saveModeService);

        var response = await sut.SetSaveMode(new PreferenceSetSaveModeRequest("cloud"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Null(saveModeService.LastSetScope);
    }

    [Fact]
    public async Task SetSaveMode_ShouldPersistMode_WhenValid()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var saveModeService = new FakeVocabularySaveModePreferenceService();
        var sut = CreateSut(scopeAccessor, saveModeService);

        var response = await sut.SetSaveMode(
            new PreferenceSetSaveModeRequest("off", "  TeLeGrAm  ", "Mike", "chat-42"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<PreferenceSaveModeResponse>(ok.Value);

        Assert.Equal("off", payload.Mode);
        Assert.Equal(VocabularySaveMode.Off, saveModeService.CurrentMode);
        Assert.NotNull(saveModeService.LastSetScope);
        Assert.Equal("telegram", saveModeService.LastSetScope!.Channel);
        Assert.Equal("mike", saveModeService.LastSetScope!.UserId);
        Assert.Equal("chat-42", saveModeService.LastSetScope!.ConversationId);
    }

    [Fact]
    public async Task GetSession_ShouldReturnBothModes_ForScope()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var saveModeService = new FakeVocabularySaveModePreferenceService
        {
            CurrentMode = VocabularySaveMode.Off
        };
        var storagePreferenceService = new FakeVocabularyStoragePreferenceService
        {
            CurrentMode = VocabularyStorageMode.Graph
        };
        var storageModeProvider = new FakeVocabularyStorageModeProvider();
        var sut = new PreferencesController(scopeAccessor, saveModeService, storagePreferenceService, storageModeProvider);

        var response = await sut.GetSession("telegram", "mike", "chat-42", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<PreferenceSessionResponse>(ok.Value);

        Assert.Equal("off", payload.SaveMode);
        Assert.Equal("graph", payload.StorageMode);
        Assert.Equal(["ask", "auto", "off"], payload.AvailableSaveModes);
        Assert.Equal(["local", "graph"], payload.AvailableStorageModes);
        Assert.Equal(VocabularyStorageMode.Graph, storageModeProvider.CurrentMode);
    }

    [Fact]
    public async Task SetSession_ShouldReturnBadRequest_WhenNoValuesProvided()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var saveModeService = new FakeVocabularySaveModePreferenceService();
        var storagePreferenceService = new FakeVocabularyStoragePreferenceService();
        var storageModeProvider = new FakeVocabularyStorageModeProvider();
        var sut = new PreferencesController(scopeAccessor, saveModeService, storagePreferenceService, storageModeProvider);

        var response = await sut.SetSession(new PreferenceSetSessionRequest(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    [Fact]
    public async Task SetSession_ShouldPersistBothModes_WhenValid()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var saveModeService = new FakeVocabularySaveModePreferenceService();
        var storagePreferenceService = new FakeVocabularyStoragePreferenceService();
        var storageModeProvider = new FakeVocabularyStorageModeProvider();
        var sut = new PreferencesController(scopeAccessor, saveModeService, storagePreferenceService, storageModeProvider);

        var response = await sut.SetSession(
            new PreferenceSetSessionRequest(
                SaveMode: "auto",
                StorageMode: "graph",
                Channel: " telegram ",
                UserId: "Mike",
                ConversationId: "chat-42"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<PreferenceSessionResponse>(ok.Value);

        Assert.Equal("auto", payload.SaveMode);
        Assert.Equal("graph", payload.StorageMode);
        Assert.Equal(VocabularySaveMode.Auto, saveModeService.CurrentMode);
        Assert.Equal(VocabularyStorageMode.Graph, storagePreferenceService.CurrentMode);
        Assert.Equal(VocabularyStorageMode.Graph, storageModeProvider.CurrentMode);

        Assert.NotNull(saveModeService.LastSetScope);
        Assert.Equal("telegram", saveModeService.LastSetScope!.Channel);
        Assert.Equal("mike", saveModeService.LastSetScope!.UserId);

        Assert.NotNull(storagePreferenceService.LastSetScope);
        Assert.Equal("telegram", storagePreferenceService.LastSetScope!.Channel);
        Assert.Equal("mike", storagePreferenceService.LastSetScope!.UserId);
    }

    [Fact]
    public async Task SetSession_ShouldReturnBadRequest_WhenStorageModeUnsupported()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var saveModeService = new FakeVocabularySaveModePreferenceService();
        var storagePreferenceService = new FakeVocabularyStoragePreferenceService();
        var storageModeProvider = new FakeVocabularyStorageModeProvider();
        var sut = new PreferencesController(scopeAccessor, saveModeService, storagePreferenceService, storageModeProvider);

        var response = await sut.SetSession(
            new PreferenceSetSessionRequest(SaveMode: "ask", StorageMode: "cloud"),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    private static PreferencesController CreateSut(
        FakeConversationScopeAccessor scopeAccessor,
        FakeVocabularySaveModePreferenceService saveModeService)
    {
        return new PreferencesController(
            scopeAccessor,
            saveModeService,
            new FakeVocabularyStoragePreferenceService(),
            new FakeVocabularyStorageModeProvider());
    }

    private sealed class FakeConversationScopeAccessor : IConversationScopeAccessor
    {
        public ConversationScope Current { get; private set; } = ConversationScope.Default;

        public void Set(ConversationScope scope)
        {
            Current = scope;
        }
    }

    private sealed class FakeVocabularySaveModePreferenceService : IVocabularySaveModePreferenceService
    {
        public IReadOnlyList<string> SupportedModes { get; } = ["ask", "auto", "off"];

        public VocabularySaveMode CurrentMode { get; set; } = VocabularySaveMode.Ask;

        public ConversationScope? LastGetScope { get; private set; }

        public ConversationScope? LastSetScope { get; private set; }

        public bool TryParse(string? value, out VocabularySaveMode mode)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "ask":
                    mode = VocabularySaveMode.Ask;
                    return true;
                case "auto":
                    mode = VocabularySaveMode.Auto;
                    return true;
                case "off":
                    mode = VocabularySaveMode.Off;
                    return true;
                default:
                    mode = VocabularySaveMode.Ask;
                    return false;
            }
        }

        public string ToText(VocabularySaveMode mode)
        {
            return mode.ToString().ToLowerInvariant();
        }

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

    private sealed class FakeVocabularyStoragePreferenceService : IVocabularyStoragePreferenceService
    {
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

    private sealed class FakeVocabularyStorageModeProvider : IVocabularyStorageModeProvider
    {
        public VocabularyStorageMode CurrentMode { get; set; } = VocabularyStorageMode.Local;

        public void SetMode(VocabularyStorageMode mode)
        {
            CurrentMode = mode;
        }

        public bool TryParse(string? value, out VocabularyStorageMode mode)
        {
            if (string.Equals(value, "local", StringComparison.OrdinalIgnoreCase))
            {
                mode = VocabularyStorageMode.Local;
                return true;
            }

            if (string.Equals(value, "graph", StringComparison.OrdinalIgnoreCase))
            {
                mode = VocabularyStorageMode.Graph;
                return true;
            }

            mode = VocabularyStorageMode.Local;
            return false;
        }

        public string ToText(VocabularyStorageMode mode)
        {
            return mode.ToString().ToLowerInvariant();
        }
    }
}
