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
        var sut = new PreferencesController(scopeAccessor, saveModeService);

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
        var sut = new PreferencesController(scopeAccessor, saveModeService);

        var response = await sut.SetSaveMode(new PreferenceSetSaveModeRequest("   "), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Null(saveModeService.LastSetScope);
    }

    [Fact]
    public async Task SetSaveMode_ShouldReturnBadRequest_WhenModeUnsupported()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var saveModeService = new FakeVocabularySaveModePreferenceService();
        var sut = new PreferencesController(scopeAccessor, saveModeService);

        var response = await sut.SetSaveMode(new PreferenceSetSaveModeRequest("cloud"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Null(saveModeService.LastSetScope);
    }

    [Fact]
    public async Task SetSaveMode_ShouldPersistMode_WhenValid()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var saveModeService = new FakeVocabularySaveModePreferenceService();
        var sut = new PreferencesController(scopeAccessor, saveModeService);

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
}
