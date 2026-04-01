namespace LagerthaAssistant.Api.Controllers;

using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Interfaces.Common;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/preferences/locale")]
public sealed class PreferenceLocaleController : ControllerBase
{
    private static readonly IReadOnlyList<string> AvailableLocales =
        [LocalizationConstants.UkrainianLocale, LocalizationConstants.EnglishLocale];

    private readonly IConversationScopeAccessor _scopeAccessor;
    private readonly IUserLocaleStateService _localeStateService;

    public PreferenceLocaleController(
        IConversationScopeAccessor scopeAccessor,
        IUserLocaleStateService localeStateService)
    {
        _scopeAccessor = scopeAccessor;
        _localeStateService = localeStateService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PreferenceLocaleResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PreferenceLocaleResponse>> GetLocale(
        [FromQuery] string? channel = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        var scope = ApiConversationScopeApplier.Apply(_scopeAccessor, channel, userId, conversationId);
        var storedLocale = await _localeStateService.GetStoredLocaleAsync(scope.Channel, scope.UserId, cancellationToken);

        // Mini App policy (v1): when locale is not persisted yet, default to Ukrainian.
        var locale = string.IsNullOrWhiteSpace(storedLocale)
            ? LocalizationConstants.UkrainianLocale
            : LocalizationConstants.NormalizeLocaleCode(storedLocale);

        return Ok(new PreferenceLocaleResponse(locale, AvailableLocales));
    }

    [HttpPut]
    [ProducesResponseType(typeof(PreferenceLocaleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PreferenceLocaleResponse>> SetLocale(
        [FromBody] PreferenceSetLocaleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Locale))
        {
            return BadRequest("Locale is required.");
        }

        if (!TryParseLocale(request.Locale, out var locale))
        {
            return BadRequest(ApiModeValidationErrors.BuildUnsupported("locale", request.Locale, AvailableLocales));
        }

        var scope = ApiConversationScopeApplier.Apply(_scopeAccessor, request.Channel, request.UserId, request.ConversationId);
        var persisted = await _localeStateService.SetLocaleAsync(
            scope.Channel,
            scope.UserId,
            locale,
            request.SelectedManually,
            cancellationToken);

        return Ok(new PreferenceLocaleResponse(persisted, AvailableLocales));
    }

    private static bool TryParseLocale(string value, out string locale)
    {
        locale = LocalizationConstants.EnglishLocale;

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.StartsWith("uk", StringComparison.Ordinal)
            || normalized.StartsWith("ua", StringComparison.Ordinal)
            || normalized.StartsWith("ru", StringComparison.Ordinal)
            || normalized.StartsWith("be", StringComparison.Ordinal))
        {
            locale = LocalizationConstants.UkrainianLocale;
            return true;
        }

        if (normalized.StartsWith("en", StringComparison.Ordinal))
        {
            locale = LocalizationConstants.EnglishLocale;
            return true;
        }

        return false;
    }
}
