using System.Globalization;
using System.Net;
using System.Text.Json;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Api.Services;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Navigation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SharedBotKernel.Abstractions;
using SharedBotKernel.Infrastructure.Telegram;
using SharedBotKernel.Models.Agents;
using SharedBotKernel.Options;

namespace LagerthaAssistant.Api.Controllers;

[ApiController]
[Route("api/miniapp/settings")]
public sealed class MiniAppSettingsController : ControllerBase
{
    private static readonly TimeSpan MaxInitDataAge = TimeSpan.FromHours(24);

    private readonly IConversationScopeAccessor _scopeAccessor;
    private readonly MiniAppSettingsCommitService _commitService;
    private readonly INavigationStateService _navigationStateService;
    private readonly ITelegramNavigationPresenter _navigationPresenter;
    private readonly ITelegramBotSender _telegramBotSender;
    private readonly TelegramOptions _telegramOptions;

    public MiniAppSettingsController(
        IConversationScopeAccessor scopeAccessor,
        MiniAppSettingsCommitService commitService,
        INavigationStateService navigationStateService,
        ITelegramNavigationPresenter navigationPresenter,
        ITelegramBotSender telegramBotSender,
        IOptions<TelegramOptions> telegramOptions)
    {
        _scopeAccessor = scopeAccessor;
        _commitService = commitService;
        _navigationStateService = navigationStateService;
        _navigationPresenter = navigationPresenter;
        _telegramBotSender = telegramBotSender;
        _telegramOptions = telegramOptions.Value;
    }

    [HttpPost("commit")]
    [ProducesResponseType(typeof(MiniAppSettingsCommitResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MiniAppSettingsCommitResponse>> Commit(
        [FromBody] MiniAppSettingsCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        if (!TryResolveScope(request, out var scope, out var scopeError))
        {
            return BadRequest(scopeError);
        }

        var result = await _commitService.CommitAsync(scope, request, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(result.ErrorMessage);
        }

        if (result.Response is not null)
        {
            await TryRefreshTelegramMainKeyboardAsync(scope, result.Response.Locale, cancellationToken);
        }

        return Ok(result.Response);
    }

    private bool TryResolveScope(
        MiniAppSettingsCommitRequest request,
        out ConversationScope scope,
        out string? errorMessage)
    {
        scope = ConversationScope.Default;
        errorMessage = null;

        if (!string.Equals(request.Channel, "telegram", StringComparison.OrdinalIgnoreCase))
        {
            scope = ApiConversationScopeApplier.Apply(
                _scopeAccessor,
                request.Channel,
                request.UserId,
                request.ConversationId);
            return true;
        }

        if (string.IsNullOrWhiteSpace(request.InitData))
        {
            errorMessage = "initData is required for Telegram-scoped writes.";
            return false;
        }

        var verification = TelegramMiniAppInitDataVerifier.Verify(
            request.InitData,
            _telegramOptions.BotToken,
            DateTimeOffset.UtcNow,
            MaxInitDataAge);

        if (!verification.IsValid)
        {
            errorMessage = $"initData is invalid: {verification.Reason}.";
            return false;
        }

        if (!TryParseTelegramUserId(request.InitData, out var verifiedUserId))
        {
            errorMessage = "initData user is missing.";
            return false;
        }

        var verifiedConversationId = ResolveVerifiedTelegramConversationId(
            request.ConversationId,
            verifiedUserId);
        if (verifiedConversationId is null)
        {
            errorMessage = "conversationId does not match verified Telegram user.";
            return false;
        }

        scope = ApiConversationScopeApplier.Apply(
            _scopeAccessor,
            "telegram",
            verifiedUserId,
            verifiedConversationId);
        return true;
    }

    private async Task TryRefreshTelegramMainKeyboardAsync(
        ConversationScope scope,
        string locale,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(scope.Channel, "telegram", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!TryResolveTelegramTarget(scope, out var chatId, out var messageThreadId))
        {
            return;
        }

        await _navigationStateService.SetCurrentSectionAsync(
            scope.Channel,
            scope.UserId,
            scope.ConversationId,
            NavigationSections.Main,
            cancellationToken);

        var text = WebUtility.HtmlEncode(_navigationPresenter.GetText("menu.main.title", locale));
        var options = new TelegramSendOptions(
            ParseMode: "HTML",
            ReplyMarkup: _navigationPresenter.BuildMainReplyKeyboard(locale));

        var result = await _telegramBotSender.SendTextAsync(
            chatId,
            text,
            options,
            messageThreadId,
            cancellationToken);

        if (!result.Succeeded)
        {
            // Silent failure: settings are already persisted, this is only UI refresh.
        }
    }

    private static bool TryResolveTelegramTarget(
        SharedBotKernel.Models.Agents.ConversationScope scope,
        out long chatId,
        out int? messageThreadId)
    {
        chatId = 0;
        messageThreadId = null;

        if (!string.IsNullOrWhiteSpace(scope.ConversationId)
            && !string.Equals(scope.ConversationId, SharedBotKernel.Models.Agents.ConversationScope.DefaultConversationId, StringComparison.OrdinalIgnoreCase))
        {
            var parts = scope.ConversationId.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length >= 1
                && long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out chatId))
            {
                if (parts.Length == 2
                    && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedThreadId))
                {
                    messageThreadId = parsedThreadId;
                }

                return true;
            }
        }

        return long.TryParse(scope.UserId, NumberStyles.Integer, CultureInfo.InvariantCulture, out chatId);
    }

    private static string? ResolveVerifiedTelegramConversationId(
        string? requestedConversationId,
        string verifiedUserId)
    {
        if (string.IsNullOrWhiteSpace(requestedConversationId)
            || string.Equals(requestedConversationId.Trim(), ConversationScope.DefaultConversationId, StringComparison.OrdinalIgnoreCase))
        {
            return verifiedUserId;
        }

        var normalizedConversationId = requestedConversationId.Trim().ToLowerInvariant();
        if (string.Equals(normalizedConversationId, verifiedUserId, StringComparison.Ordinal)
            || normalizedConversationId.StartsWith($"{verifiedUserId}:", StringComparison.Ordinal))
        {
            return normalizedConversationId;
        }

        return null;
    }

    private static bool TryParseTelegramUserId(string initData, out string userId)
    {
        userId = string.Empty;

        try
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in initData.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var idx = pair.IndexOf('=');
                if (idx <= 0)
                {
                    continue;
                }

                var key = Uri.UnescapeDataString(pair[..idx]);
                var value = Uri.UnescapeDataString(pair[(idx + 1)..]);
                parameters[key] = value;
            }

            if (!parameters.TryGetValue("user", out var userPayload) || string.IsNullOrWhiteSpace(userPayload))
            {
                return false;
            }

            using var document = JsonDocument.Parse(userPayload);
            if (!document.RootElement.TryGetProperty("id", out var idElement))
            {
                return false;
            }

            userId = idElement.ValueKind switch
            {
                JsonValueKind.Number when idElement.TryGetInt64(out var numericId)
                    => numericId.ToString(CultureInfo.InvariantCulture),
                JsonValueKind.String => idElement.GetString()?.Trim() ?? string.Empty,
                _ => string.Empty
            };

            return !string.IsNullOrWhiteSpace(userId);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
