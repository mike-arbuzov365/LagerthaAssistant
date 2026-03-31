namespace BaguetteDesign.Application.Services;

using BaguetteDesign.Application.Interfaces;
using Microsoft.Extensions.Logging;
using SharedBotKernel.Domain.AI;

public sealed class CommercialProposalHandler : ICommercialProposalHandler
{
    private const string ProposalDraftPrefix = "kp_draft_";

    private readonly ILeadRepository _leads;
    private readonly IUserMemoryRepository _memory;
    private readonly IAiChatClient _ai;
    private readonly ITelegramBotSender _sender;
    private readonly ILogger<CommercialProposalHandler> _logger;

    public CommercialProposalHandler(
        ILeadRepository leads,
        IUserMemoryRepository memory,
        IAiChatClient ai,
        ITelegramBotSender sender,
        ILogger<CommercialProposalHandler> logger)
    {
        _leads = leads;
        _memory = memory;
        _ai = ai;
        _sender = sender;
        _logger = logger;
    }

    public async Task GenerateDraftAsync(long chatId, int leadId, string? languageCode, CancellationToken ct = default)
    {
        var locale = ResolveLocale(languageCode);
        var lead = await _leads.GetByIdAsync(leadId, ct);

        if (lead is null)
        {
            await _sender.SendTextAsync(chatId,
                locale == "uk" ? "⚠️ Лід не знайдено." : "⚠️ Lead not found.",
                cancellationToken: ct);
            return;
        }

        var generating = locale == "uk"
            ? "⏳ Генерую комерційну пропозицію..."
            : "⏳ Generating commercial proposal...";
        await _sender.SendTextAsync(chatId, generating, cancellationToken: ct);

        var systemPrompt = locale == "uk"
            ? "Ти досвідчений графічний дизайнер. Склади комерційну пропозицію (КП) українською мовою. КП повинна містити: короткий опис послуги, що входить в роботу, вартість, терміни, умови. Стиль — діловий, але дружній."
            : "You are an experienced graphic designer. Write a commercial proposal in English. Include: brief service description, scope of work, cost, timeline, terms. Style — professional but friendly.";

        var userPrompt = $"Client brief:\n" +
            $"Service: {lead.ServiceType}\n" +
            $"Brand: {lead.Brand}\n" +
            $"Audience: {lead.Audience}\n" +
            $"Style: {lead.Style}\n" +
            $"Deadline: {lead.Deadline}\n" +
            $"Budget: {lead.Budget}\n" +
            $"Country: {lead.Country}\n" +
            (string.IsNullOrEmpty(lead.AiSummary) ? string.Empty : $"Summary: {lead.AiSummary}\n") +
            "\nGenerate a commercial proposal:";

        string draft;
        try
        {
            var messages = new[]
            {
                ConversationMessage.Create(MessageRole.System, systemPrompt, DateTimeOffset.UtcNow),
                ConversationMessage.Create(MessageRole.User, userPrompt, DateTimeOffset.UtcNow)
            };
            var result = await _ai.CompleteAsync(messages, ct);
            draft = result.Content;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "AI draft generation failed for lead {LeadId}; using template fallback.", leadId);
            draft = locale == "uk"
                ? $"Комерційна пропозиція для {lead.ServiceType ?? "дизайн-проєкту"}\n\nБюджет: {lead.Budget}\nТермін: {lead.Deadline}"
                : $"Commercial Proposal for {lead.ServiceType ?? "design project"}\n\nBudget: {lead.Budget}\nTimeline: {lead.Deadline}";
        }

        await _memory.SetAsync(chatId.ToString(), $"{ProposalDraftPrefix}{leadId}", draft, ct);

        var keyboard = new
        {
            inline_keyboard = new[]
            {
                new[]
                {
                    Btn(locale == "uk" ? "✅ Надіслати клієнту" : "✅ Send to client", $"kp_send_{leadId}"),
                    Btn(locale == "uk" ? "❌ Відхилити" : "❌ Dismiss", $"kp_dismiss_{leadId}")
                }
            }
        };

        var msg = locale == "uk"
            ? $"📝 <b>Чернетка КП для ліда #{leadId}:</b>\n\n{draft}"
            : $"📝 <b>Draft proposal for lead #{leadId}:</b>\n\n{draft}";

        await _sender.SendTextAsync(chatId, msg,
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: keyboard), cancellationToken: ct);
    }

    public async Task SendProposalAsync(long chatId, long designerUserId, int leadId, string? languageCode, CancellationToken ct = default)
    {
        var locale = ResolveLocale(languageCode);
        var draft = await _memory.GetAsync(chatId.ToString(), $"{ProposalDraftPrefix}{leadId}", ct);

        if (string.IsNullOrWhiteSpace(draft))
        {
            await _sender.SendTextAsync(chatId,
                locale == "uk" ? "⚠️ Чернетка не знайдена." : "⚠️ Draft not found.",
                cancellationToken: ct);
            return;
        }

        var lead = await _leads.GetByIdAsync(leadId, ct);
        if (lead is not null && long.TryParse(lead.UserId, out var clientChatId))
            await _sender.SendTextAsync(clientChatId, draft, cancellationToken: ct);

        await _memory.DeleteAsync(chatId.ToString(), $"{ProposalDraftPrefix}{leadId}", ct);

        var confirm = locale == "uk"
            ? $"✅ КП надіслано клієнту."
            : $"✅ Proposal sent to client.";
        await _sender.SendTextAsync(chatId, confirm, cancellationToken: ct);
    }

    public async Task DismissProposalAsync(long chatId, long designerUserId, int leadId, string? languageCode, CancellationToken ct = default)
    {
        var locale = ResolveLocale(languageCode);
        await _memory.DeleteAsync(chatId.ToString(), $"{ProposalDraftPrefix}{leadId}", ct);

        await _sender.SendTextAsync(chatId,
            locale == "uk" ? "🗑️ Чернетку КП відхилено." : "🗑️ Proposal draft dismissed.",
            cancellationToken: ct);
    }

    private static object Btn(string text, string callbackData)
        => new { text, callback_data = callbackData };

    private static string ResolveLocale(string? languageCode)
        => languageCode?.StartsWith("uk", StringComparison.OrdinalIgnoreCase) == true ? "uk" : "en";
}
