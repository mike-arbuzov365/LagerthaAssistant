using System.Globalization;
using System.Net;
using LagerthaAssistant.Api.Contracts;
using LagerthaAssistant.Api.Interfaces;
using LagerthaAssistant.Api.Services;
using LagerthaAssistant.Application.Constants;
using LagerthaAssistant.Application.Interfaces;
using LagerthaAssistant.Application.Navigation;
using Microsoft.AspNetCore.Mvc;
using SharedBotKernel.Abstractions;

namespace LagerthaAssistant.Api.Controllers;

[ApiController]
[Route("api/miniapp/settings")]
public sealed class MiniAppSettingsController : ControllerBase
{
    private readonly IConversationScopeAccessor _scopeAccessor;
    private readonly MiniAppSettingsCommitService _commitService;
    private readonly INavigationStateService _navigationStateService;
    private readonly ITelegramNavigationPresenter _navigationPresenter;
    private readonly ITelegramBotSender _telegramBotSender;

    public MiniAppSettingsController(
        IConversationScopeAccessor scopeAccessor,
        MiniAppSettingsCommitService commitService,
        INavigationStateService navigationStateService,
        ITelegramNavigationPresenter navigationPresenter,
        ITelegramBotSender telegramBotSender)
    {
        _scopeAccessor = scopeAccessor;
        _commitService = commitService;
        _navigationStateService = navigationStateService;
        _navigationPresenter = navigationPresenter;
        _telegramBotSender = telegramBotSender;
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

        var scope = ApiConversationScopeApplier.Apply(
            _scopeAccessor,
            request.Channel,
            request.UserId,
            request.ConversationId);

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

    private async Task TryRefreshTelegramMainKeyboardAsync(
        SharedBotKernel.Models.Agents.ConversationScope scope,
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
}
