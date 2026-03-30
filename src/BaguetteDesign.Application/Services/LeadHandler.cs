namespace BaguetteDesign.Application.Services;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Enums;
using SharedBotKernel.Infrastructure.Telegram;

public sealed class LeadHandler : ILeadHandler
{
    private readonly ILeadService _leadService;
    private readonly ITelegramBotSender _sender;

    public LeadHandler(ILeadService leadService, ITelegramBotSender sender)
    {
        _leadService = leadService;
        _sender = sender;
    }

    public async Task ShowLeadsAsync(long chatId, string? languageCode, CancellationToken ct = default)
    {
        var locale = ResolveLocale(languageCode);
        var leads = await _leadService.GetLeadsAsync(ct: ct);

        if (leads.Count == 0)
        {
            var empty = locale == "uk"
                ? "👤 <b>Ліди</b>\n\nПоки що немає лідів."
                : "👤 <b>Leads</b>\n\nNo leads yet.";
            await _sender.SendTextAsync(chatId, empty,
                new TelegramSendOptions(ParseMode: "HTML"), cancellationToken: ct);
            return;
        }

        var header = locale == "uk" ? "👤 <b>Ліди</b>\n" : "👤 <b>Leads</b>\n";
        var buttons = leads.Take(10).Select(l =>
        {
            var icon = StatusIcon(l.Status);
            var label = $"{icon} #{l.Id} — {l.UserId} | {l.ServiceType ?? "?"} | {StatusLabel(l.Status, locale)}";
            return new[] { Btn(label, $"lead_card_{l.Id}") };
        }).ToArray<object[]>();

        await _sender.SendTextAsync(chatId, header,
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: new { inline_keyboard = buttons }),
            cancellationToken: ct);
    }

    public async Task ShowLeadCardAsync(long chatId, int leadId, string? languageCode, CancellationToken ct = default)
    {
        var locale = ResolveLocale(languageCode);
        var lead = await _leadService.GetByIdAsync(leadId, ct);

        if (lead is null)
        {
            var notFound = locale == "uk" ? "⚠️ Лід не знайдено." : "⚠️ Lead not found.";
            await _sender.SendTextAsync(chatId, notFound, cancellationToken: ct);
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(locale == "uk" ? $"👤 <b>Лід #{lead.Id}</b>" : $"👤 <b>Lead #{lead.Id}</b>");
        sb.AppendLine($"{StatusIcon(lead.Status)} {StatusLabel(lead.Status, locale)}");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(lead.ServiceType)) sb.AppendLine($"🎨 {(locale == "uk" ? "Послуга" : "Service")}: {lead.ServiceType}");
        if (!string.IsNullOrEmpty(lead.Brand)) sb.AppendLine($"🏷️ {(locale == "uk" ? "Бренд" : "Brand")}: {lead.Brand}");
        if (!string.IsNullOrEmpty(lead.Audience)) sb.AppendLine($"👥 {(locale == "uk" ? "Аудиторія" : "Audience")}: {lead.Audience}");
        if (!string.IsNullOrEmpty(lead.Style)) sb.AppendLine($"✏️ {(locale == "uk" ? "Стиль" : "Style")}: {lead.Style}");
        if (!string.IsNullOrEmpty(lead.Deadline)) sb.AppendLine($"📅 {(locale == "uk" ? "Дедлайн" : "Deadline")}: {lead.Deadline}");
        if (!string.IsNullOrEmpty(lead.Budget)) sb.AppendLine($"💰 {(locale == "uk" ? "Бюджет" : "Budget")}: {lead.Budget}");
        if (!string.IsNullOrEmpty(lead.Country)) sb.AppendLine($"🌍 {(locale == "uk" ? "Країна" : "Country")}: {lead.Country}");
        if (!string.IsNullOrEmpty(lead.AiSummary)) sb.AppendLine($"\n💬 {lead.AiSummary}");

        var keyboard = new
        {
            inline_keyboard = new[]
            {
                new[]
                {
                    Btn("🟡 New", $"lead_status_{leadId}_new"),
                    Btn("🔵 In Progress", $"lead_status_{leadId}_inprogress"),
                    Btn("⏳ Waiting", $"lead_status_{leadId}_waiting")
                },
                new[]
                {
                    Btn("🟢 Converted", $"lead_status_{leadId}_converted"),
                    Btn("⚪ Closed", $"lead_status_{leadId}_closed")
                },
                new[]
                {
                    Btn(locale == "uk" ? "💬 Відкрити діалог" : "💬 Open dialog", $"inbox_open_{lead.UserId}")
                }
            }
        };

        await _sender.SendTextAsync(chatId, sb.ToString().TrimEnd(),
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: keyboard), cancellationToken: ct);
    }

    public async Task ChangeLeadStatusAsync(long chatId, int leadId, string newStatus, string? languageCode, CancellationToken ct = default)
    {
        var locale = ResolveLocale(languageCode);
        var status = newStatus.ToLowerInvariant() switch
        {
            "new" => LeadStatus.New,
            "inprogress" => LeadStatus.InProgress,
            "waiting" => LeadStatus.WaitingMaterials,
            "converted" => LeadStatus.Converted,
            "closed" => LeadStatus.Closed,
            _ => LeadStatus.New
        };

        await _leadService.ChangeStatusAsync(leadId, status, ct);

        var icon = StatusIcon(status);
        var msg = locale == "uk"
            ? $"{icon} Статус ліда #{leadId} змінено на {StatusLabel(status, locale)}."
            : $"{icon} Lead #{leadId} status changed to {StatusLabel(status, locale)}.";
        await _sender.SendTextAsync(chatId, msg, cancellationToken: ct);
    }

    private static string StatusIcon(LeadStatus status) => status switch
    {
        LeadStatus.New => "🟡",
        LeadStatus.InProgress => "🔵",
        LeadStatus.WaitingMaterials => "⏳",
        LeadStatus.Converted => "🟢",
        LeadStatus.Closed => "⚪",
        _ => "❓"
    };

    private static string StatusLabel(LeadStatus status, string locale) => locale == "uk"
        ? status switch
        {
            LeadStatus.New => "Новий",
            LeadStatus.InProgress => "В роботі",
            LeadStatus.WaitingMaterials => "Очікуємо матеріали",
            LeadStatus.Converted => "Конвертовано",
            LeadStatus.Closed => "Закрито",
            _ => "?"
        }
        : status switch
        {
            LeadStatus.New => "New",
            LeadStatus.InProgress => "In Progress",
            LeadStatus.WaitingMaterials => "Waiting Materials",
            LeadStatus.Converted => "Converted",
            LeadStatus.Closed => "Closed",
            _ => "?"
        };

    private static object Btn(string text, string callbackData)
        => new { text, callback_data = callbackData };

    private static string ResolveLocale(string? languageCode)
        => languageCode?.StartsWith("uk", StringComparison.OrdinalIgnoreCase) == true ? "uk" : "en";
}
