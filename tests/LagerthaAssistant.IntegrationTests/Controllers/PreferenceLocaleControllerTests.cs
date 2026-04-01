namespace LagerthaAssistant.IntegrationTests.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Controllers;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Interfaces.Common;
using LagerthaAssistant.Application.Models.Localization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class PreferenceLocaleControllerTests
{
    [Fact]
    public async Task GetLocale_ShouldUseUkrainianFallback_WhenLocaleMissing()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var localeService = new FakeUserLocaleStateService
        {
            StoredLocale = null
        };
        var sut = new PreferenceLocaleController(scopeAccessor, localeService);

        var response = await sut.GetLocale(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<PreferenceLocaleResponse>(ok.Value);

        Assert.Equal("uk", payload.Locale);
        Assert.Equal(["uk", "en"], payload.AvailableLocales);
        Assert.Equal("api", scopeAccessor.Current.Channel);
        Assert.Equal("anonymous", scopeAccessor.Current.UserId);
    }

    [Fact]
    public async Task SetLocale_ShouldReturnBadRequest_WhenLocaleMissing()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var localeService = new FakeUserLocaleStateService();
        var sut = new PreferenceLocaleController(scopeAccessor, localeService);

        var response = await sut.SetLocale(new PreferenceSetLocaleRequest("  "), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Null(localeService.LastSetCall);
    }

    [Fact]
    public async Task SetLocale_ShouldReturnBadRequest_WhenLocaleUnsupported()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var localeService = new FakeUserLocaleStateService();
        var sut = new PreferenceLocaleController(scopeAccessor, localeService);

        var response = await sut.SetLocale(new PreferenceSetLocaleRequest("de-DE"), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal("Unsupported locale 'de-DE'. Use one of: uk, en.", badRequest.Value);
        Assert.Null(localeService.LastSetCall);
    }

    [Fact]
    public async Task SetLocale_ShouldPersistLocaleForRequestedScope()
    {
        var scopeAccessor = new FakeConversationScopeAccessor();
        var localeService = new FakeUserLocaleStateService
        {
            SetLocaleResult = "en"
        };
        var sut = new PreferenceLocaleController(scopeAccessor, localeService);

        var response = await sut.SetLocale(
            new PreferenceSetLocaleRequest(
                Locale: "en-US",
                SelectedManually: true,
                Channel: "  TeLeGrAm ",
                UserId: "Mike",
                ConversationId: "chat-42"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var payload = Assert.IsType<PreferenceLocaleResponse>(ok.Value);

        Assert.Equal("en", payload.Locale);
        Assert.NotNull(localeService.LastSetCall);
        Assert.Equal("telegram", localeService.LastSetCall!.Channel);
        Assert.Equal("mike", localeService.LastSetCall!.UserId);
        Assert.Equal("en", localeService.LastSetCall!.Locale);
        Assert.True(localeService.LastSetCall!.SelectedManually);
    }

    private sealed class FakeConversationScopeAccessor : IConversationScopeAccessor
    {
        public ConversationScope Current { get; private set; } = ConversationScope.Default;

        public void Set(ConversationScope scope)
        {
            Current = scope;
        }
    }

    private sealed class FakeUserLocaleStateService : IUserLocaleStateService
    {
        public string? StoredLocale { get; set; }

        public string SetLocaleResult { get; set; } = "uk";

        public SetLocaleCall? LastSetCall { get; private set; }

        public Task<string?> GetStoredLocaleAsync(
            string channel,
            string userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(StoredLocale);
        }

        public Task<string> SetLocaleAsync(
            string channel,
            string userId,
            string locale,
            bool selectedManually,
            CancellationToken cancellationToken = default)
        {
            LastSetCall = new SetLocaleCall(channel, userId, locale, selectedManually);
            return Task.FromResult(SetLocaleResult);
        }

        public Task<UserLocaleStateResult> EnsureLocaleAsync(
            string channel,
            string userId,
            string? telegramLanguageCode,
            string? incomingText,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UserLocaleStateResult("uk", IsInitialized: false, IsSwitched: false));
        }
    }

    private sealed record SetLocaleCall(
        string Channel,
        string UserId,
        string Locale,
        bool SelectedManually);
}
