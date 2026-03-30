namespace BaguetteDesign.Api.Controllers;

using System.Text.Json.Serialization;
using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Enums;
using BaguetteDesign.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/telegram")]
public sealed class TelegramController : ControllerBase
{
    private const string PriceCategoryPrefix = "price_cat_";
    private const string PortfolioCategoryPrefix = "portfolio_cat_";
    private const string PortfolioSimilarPrefix = "portfolio_similar_";
    private const string ContactSlotPrefix = "contact_slot_";
    private const string InboxOpenPrefix = "inbox_open_";
    private const string InboxSendPrefix = "inbox_send_";
    private const string InboxDismissPrefix = "inbox_dismiss_";
    private const string InboxManualPrefix = "inbox_manual_";
    private const string InboxAutoPrefix = "inbox_auto_";
    private const string InboxStatusPrefix = "inbox_status_";
    private const string LeadCardPrefix = "lead_card_";
    private const string LeadStatusPrefix = "lead_status_";
    private const string ProjectCardPrefix = "project_card_";
    private const string ProjectStatusPrefix = "project_status_";
    private const string ProjectRevisionPrefix = "project_revision_";
    private const string LeadConvertPrefix = "lead_convert_";
    private const string KpGeneratePrefix = "kp_generate_";
    private const string KpSendPrefix = "kp_send_";
    private const string KpDismissPrefix = "kp_dismiss_";

    private readonly IStartCommandHandler _startHandler;
    private readonly IQuestionHandler _questionHandler;
    private readonly IBriefFlowService _briefFlow;
    private readonly IPriceHandler _priceHandler;
    private readonly IPortfolioHandler _portfolioHandler;
    private readonly IContactHandler _contactHandler;
    private readonly IStatusHandler _statusHandler;
    private readonly IInboxHandler _inboxHandler;
    private readonly ILeadHandler _leadHandler;
    private readonly IProjectHandler _projectHandler;
    private readonly ICommercialProposalHandler _proposalHandler;
    private readonly IRoleRouter _roleRouter;

    public TelegramController(
        IStartCommandHandler startHandler,
        IQuestionHandler questionHandler,
        IBriefFlowService briefFlow,
        IPriceHandler priceHandler,
        IPortfolioHandler portfolioHandler,
        IContactHandler contactHandler,
        IStatusHandler statusHandler,
        IInboxHandler inboxHandler,
        ILeadHandler leadHandler,
        IProjectHandler projectHandler,
        ICommercialProposalHandler proposalHandler,
        IRoleRouter roleRouter)
    {
        _startHandler = startHandler;
        _questionHandler = questionHandler;
        _briefFlow = briefFlow;
        _priceHandler = priceHandler;
        _portfolioHandler = portfolioHandler;
        _contactHandler = contactHandler;
        _statusHandler = statusHandler;
        _inboxHandler = inboxHandler;
        _leadHandler = leadHandler;
        _projectHandler = projectHandler;
        _proposalHandler = proposalHandler;
        _roleRouter = roleRouter;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(
        [FromBody] TelegramUpdate update,
        CancellationToken cancellationToken)
    {
        // ── Callback query (inline keyboard button press) ─────────────────
        if (update.CallbackQuery is { } cb)
        {
            var cbChatId = cb.Message?.Chat.Id ?? 0;
            var cbUserId = cb.From.Id;
            var cbData   = cb.Data ?? string.Empty;
            var cbLang   = cb.From.LanguageCode;

            if (cbData == "price")
            {
                await _priceHandler.ShowCategoriesAsync(cbChatId, cbLang, cancellationToken);
            }
            else if (cbData.StartsWith(PriceCategoryPrefix, StringComparison.Ordinal))
            {
                var category = Uri.UnescapeDataString(cbData[PriceCategoryPrefix.Length..]);
                await _priceHandler.ShowCategoryItemsAsync(cbChatId, category, cbLang, cancellationToken);
            }
            else if (cbData == "portfolio")
            {
                await _portfolioHandler.ShowCategoriesAsync(cbChatId, cbLang, cancellationToken);
            }
            else if (cbData.StartsWith(PortfolioCategoryPrefix, StringComparison.Ordinal))
            {
                var category = Uri.UnescapeDataString(cbData[PortfolioCategoryPrefix.Length..]);
                await _portfolioHandler.ShowCategoryItemsAsync(cbChatId, category, cbLang, cancellationToken);
            }
            else if (cbData.StartsWith(PortfolioSimilarPrefix, StringComparison.Ordinal))
            {
                var caseTitle = Uri.UnescapeDataString(cbData[PortfolioSimilarPrefix.Length..]);
                await _briefFlow.StartWithStyleAsync(cbChatId, cbUserId.ToString(), caseTitle, cbLang, cancellationToken);
            }
            else if (cbData == "status")
            {
                await _statusHandler.ShowStatusAsync(cbChatId, cbUserId, cbLang, cancellationToken);
            }
            else if (cbData == "inbox")
            {
                await _inboxHandler.ShowDialogsAsync(cbChatId, cbLang, cancellationToken);
            }
            else if (cbData.StartsWith(InboxOpenPrefix, StringComparison.Ordinal))
            {
                var clientId = cbData[InboxOpenPrefix.Length..];
                await _inboxHandler.OpenDialogAsync(cbChatId, cbUserId, clientId, cbLang, cancellationToken);
            }
            else if (cbData.StartsWith(InboxSendPrefix, StringComparison.Ordinal))
            {
                var clientId = cbData[InboxSendPrefix.Length..];
                await _inboxHandler.SendDraftAsync(cbChatId, cbUserId, clientId, cbLang, cancellationToken);
            }
            else if (cbData.StartsWith(InboxDismissPrefix, StringComparison.Ordinal))
            {
                var clientId = cbData[InboxDismissPrefix.Length..];
                await _inboxHandler.DismissDraftAsync(cbChatId, cbUserId, clientId, cbLang, cancellationToken);
            }
            else if (cbData.StartsWith(InboxManualPrefix, StringComparison.Ordinal))
            {
                var clientId = cbData[InboxManualPrefix.Length..];
                await _inboxHandler.SetManualModeAsync(cbChatId, cbUserId, clientId, cbLang, cancellationToken);
            }
            else if (cbData.StartsWith(InboxAutoPrefix, StringComparison.Ordinal))
            {
                var clientId = cbData[InboxAutoPrefix.Length..];
                await _inboxHandler.SetManualModeAsync(cbChatId, cbUserId, clientId, cbLang, cancellationToken);
            }
            else if (cbData.StartsWith(InboxStatusPrefix, StringComparison.Ordinal))
            {
                var rest = cbData[InboxStatusPrefix.Length..];
                var lastUnderscore = rest.LastIndexOf('_');
                if (lastUnderscore > 0)
                {
                    var clientId = rest[..lastUnderscore];
                    var newStatus = rest[(lastUnderscore + 1)..];
                    await _inboxHandler.ChangeDialogStatusAsync(cbChatId, clientId, newStatus, cbLang, cancellationToken);
                }
            }
            else if (cbData == "leads")
            {
                await _leadHandler.ShowLeadsAsync(cbChatId, cbLang, cancellationToken);
            }
            else if (cbData.StartsWith(LeadCardPrefix, StringComparison.Ordinal))
            {
                if (int.TryParse(cbData[LeadCardPrefix.Length..], out var leadId))
                    await _leadHandler.ShowLeadCardAsync(cbChatId, leadId, cbLang, cancellationToken);
            }
            else if (cbData.StartsWith(LeadStatusPrefix, StringComparison.Ordinal))
            {
                var rest = cbData[LeadStatusPrefix.Length..];
                var lastUnderscore = rest.LastIndexOf('_');
                if (lastUnderscore > 0 && int.TryParse(rest[..lastUnderscore], out var leadId))
                {
                    var newStatus = rest[(lastUnderscore + 1)..];
                    await _leadHandler.ChangeLeadStatusAsync(cbChatId, leadId, newStatus, cbLang, cancellationToken);
                }
            }
            else if (cbData.StartsWith(LeadConvertPrefix, StringComparison.Ordinal))
            {
                if (int.TryParse(cbData[LeadConvertPrefix.Length..], out var leadId))
                    await _projectHandler.ConvertLeadToProjectAsync(cbChatId, leadId, cbLang, cancellationToken);
            }
            else if (cbData == "projects")
            {
                await _projectHandler.ShowProjectsAsync(cbChatId, cbLang, cancellationToken);
            }
            else if (cbData.StartsWith(ProjectCardPrefix, StringComparison.Ordinal))
            {
                if (int.TryParse(cbData[ProjectCardPrefix.Length..], out var projectId))
                    await _projectHandler.ShowProjectCardAsync(cbChatId, projectId, cbLang, cancellationToken);
            }
            else if (cbData.StartsWith(ProjectRevisionPrefix, StringComparison.Ordinal))
            {
                if (int.TryParse(cbData[ProjectRevisionPrefix.Length..], out var projectId))
                    await _projectHandler.AddRevisionAsync(cbChatId, projectId, cbLang, cancellationToken);
            }
            else if (cbData.StartsWith(ProjectStatusPrefix, StringComparison.Ordinal))
            {
                var rest = cbData[ProjectStatusPrefix.Length..];
                var lastUnderscore = rest.LastIndexOf('_');
                if (lastUnderscore > 0 && int.TryParse(rest[..lastUnderscore], out var projectId))
                {
                    var newStatus = rest[(lastUnderscore + 1)..];
                    await _projectHandler.ChangeProjectStatusAsync(cbChatId, projectId, newStatus, cbLang, cancellationToken);
                }
            }
            else if (cbData.StartsWith(KpGeneratePrefix, StringComparison.Ordinal))
            {
                if (int.TryParse(cbData[KpGeneratePrefix.Length..], out var leadId))
                    await _proposalHandler.GenerateDraftAsync(cbChatId, leadId, cbLang, cancellationToken);
            }
            else if (cbData.StartsWith(KpSendPrefix, StringComparison.Ordinal))
            {
                if (int.TryParse(cbData[KpSendPrefix.Length..], out var leadId))
                    await _proposalHandler.SendProposalAsync(cbChatId, cbUserId, leadId, cbLang, cancellationToken);
            }
            else if (cbData.StartsWith(KpDismissPrefix, StringComparison.Ordinal))
            {
                if (int.TryParse(cbData[KpDismissPrefix.Length..], out var leadId))
                    await _proposalHandler.DismissProposalAsync(cbChatId, cbUserId, leadId, cbLang, cancellationToken);
            }
            else if (cbData == "contact")
            {
                await _contactHandler.ShowOptionsAsync(cbChatId, cbLang, cancellationToken);
            }
            else if (cbData == "contact_message")
            {
                await _contactHandler.PromptForMessageAsync(cbChatId, cbLang, cancellationToken);
            }
            else if (cbData == "contact_call")
            {
                await _contactHandler.ShowCalendarSlotsAsync(cbChatId, cbLang, cancellationToken);
            }
            else if (cbData.StartsWith(ContactSlotPrefix, StringComparison.Ordinal))
            {
                var slotKey = cbData[ContactSlotPrefix.Length..];
                await _contactHandler.BookSlotAsync(cbChatId, cbUserId, slotKey, cbLang, cancellationToken);
            }
            else if (cbData == "brief" || cbData.StartsWith("brief_svc_", StringComparison.Ordinal))
            {
                if (!await _briefFlow.IsActiveAsync(cbUserId.ToString(), cancellationToken))
                    await _briefFlow.StartAsync(cbChatId, cbUserId.ToString(), cbLang, cancellationToken);
                else if (cbData.StartsWith("brief_svc_", StringComparison.Ordinal))
                    await _briefFlow.HandleCallbackAsync(cbChatId, cbUserId.ToString(), cbData, cbLang, cancellationToken);
            }
            else if (cbData.StartsWith("brief_", StringComparison.Ordinal))
            {
                await _briefFlow.HandleCallbackAsync(cbChatId, cbUserId.ToString(), cbData, cbLang, cancellationToken);
            }

            return Ok();
        }

        // ── Regular message ───────────────────────────────────────────────
        var message = update.Message;
        if (message is null)
            return Ok();

        var chatId = message.Chat.Id;
        var userId = message.From?.Id ?? chatId;
        var text = message.Text?.Trim() ?? string.Empty;
        var languageCode = message.From?.LanguageCode;

        if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            await _startHandler.HandleAsync(chatId, userId, languageCode, cancellationToken);
            return Ok();
        }

        var role = _roleRouter.Resolve(userId);

        // Designer text message → check if in manual mode
        if (role == UserRole.Designer && !string.IsNullOrWhiteSpace(text))
        {
            if (await _inboxHandler.IsDesignerInManualModeAsync(userId, cancellationToken))
            {
                await _inboxHandler.HandleDesignerManualMessageAsync(chatId, userId, text, languageCode, cancellationToken);
                return Ok();
            }
            return Ok();
        }

        if (role != UserRole.Client || string.IsNullOrWhiteSpace(text))
            return Ok();

        // If awaiting message for designer
        if (await _contactHandler.IsAwaitingMessageAsync(chatId.ToString(), cancellationToken))
        {
            await _contactHandler.HandleSendMessageAsync(chatId, userId, text, languageCode, cancellationToken);
            return Ok();
        }

        // If the client is in an active brief flow, advance it
        if (await _briefFlow.IsActiveAsync(userId.ToString(), cancellationToken))
        {
            await _briefFlow.HandleTextAsync(chatId, userId.ToString(), text, languageCode, cancellationToken);
            return Ok();
        }

        // Otherwise route to AI question handler
        await _questionHandler.HandleAsync(chatId, userId, text, languageCode, cancellationToken);
        return Ok();
    }
}

// ── Telegram update models ────────────────────────────────────────────────────

public sealed record TelegramUpdate(
    [property: JsonPropertyName("update_id")]      long UpdateId,
    [property: JsonPropertyName("message")]        TelegramMessage? Message,
    [property: JsonPropertyName("callback_query")] TelegramCallbackQuery? CallbackQuery);

public sealed record TelegramMessage(
    [property: JsonPropertyName("message_id")] long MessageId,
    [property: JsonPropertyName("from")]       TelegramUser? From,
    [property: JsonPropertyName("chat")]       TelegramChat Chat,
    [property: JsonPropertyName("text")]       string? Text);

public sealed record TelegramCallbackQuery(
    [property: JsonPropertyName("id")]      string Id,
    [property: JsonPropertyName("from")]    TelegramUser From,
    [property: JsonPropertyName("message")] TelegramMessage? Message,
    [property: JsonPropertyName("data")]    string? Data);

public sealed record TelegramUser(
    [property: JsonPropertyName("id")]            long Id,
    [property: JsonPropertyName("language_code")] string? LanguageCode);

public sealed record TelegramChat(
    [property: JsonPropertyName("id")] long Id);
