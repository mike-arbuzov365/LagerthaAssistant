namespace BaguetteDesign.Application.Services;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Enums;

public sealed class StatusHandler : IStatusHandler
{
    private readonly ILeadRepository _leads;
    private readonly ITelegramBotSender _sender;

    public StatusHandler(ILeadRepository leads, ITelegramBotSender sender)
    {
        _leads = leads;
        _sender = sender;
    }

    public async Task ShowStatusAsync(long chatId, long userId, string? languageCode, CancellationToken cancellationToken = default)
    {
        var locale = ResolveLocale(languageCode);
        var lead = await _leads.GetLatestByUserIdAsync(userId.ToString(), cancellationToken);

        string text;
        object? keyboard = null;

        if (lead is null)
        {
            text = locale == "uk"
                ? "📊 <b>Статус запиту</b>\n\nУ вас ще немає жодного запиту.\n\nЗаповніть бриф і ми зв'яжемося з вами!"
                : "📊 <b>Request status</b>\n\nYou have no requests yet.\n\nFill in the brief and we'll get back to you!";

            keyboard = new
            {
                inline_keyboard = new[]
                {
                    new[] { Btn(locale == "uk" ? "📋 Заповнити бриф" : "📋 Fill in brief", "brief") }
                }
            };
        }
        else
        {
            var (statusLine, hint) = BuildStatusLine(lead.Status, lead, locale);
            var summary = string.IsNullOrWhiteSpace(lead.AiSummary)
                ? string.Empty
                : $"\n\n💬 <i>{lead.AiSummary}</i>";

            text = locale == "uk"
                ? $"📊 <b>Статус вашого запиту</b>\n\n{statusLine}{hint}{summary}"
                : $"📊 <b>Your request status</b>\n\n{statusLine}{hint}{summary}";

            keyboard = new
            {
                inline_keyboard = new[]
                {
                    new[] { Btn(locale == "uk" ? "🔗 Зв'язатися" : "🔗 Contact designer", "contact") }
                }
            };
        }

        await _sender.SendTextAsync(chatId, text,
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: keyboard),
            cancellationToken: cancellationToken);
    }

    private static (string statusLine, string hint) BuildStatusLine(
        LeadStatus status, Domain.Entities.Lead lead, string locale)
    {
        if (locale == "uk")
        {
            return status switch
            {
                LeadStatus.New =>
                    ("🟡 <b>Новий</b> — запит отримано, очікує розгляду.", string.Empty),
                LeadStatus.InProgress =>
                    ("🔵 <b>В роботі</b> — дизайнер вже працює над вашим запитом.", string.Empty),
                LeadStatus.WaitingMaterials =>
                    ("⏳ <b>Очікуємо матеріали</b>", BuildMissingHintUk(lead)),
                LeadStatus.Converted =>
                    ("🟢 <b>Проєкт створено</b> — ваш запит переведено в активний проєкт.", string.Empty),
                LeadStatus.Closed =>
                    ("⚪ <b>Закрито</b> — запит завершено.", string.Empty),
                _ =>
                    ("❓ Статус невідомий.", string.Empty)
            };
        }
        else
        {
            return status switch
            {
                LeadStatus.New =>
                    ("🟡 <b>New</b> — request received, awaiting review.", string.Empty),
                LeadStatus.InProgress =>
                    ("🔵 <b>In progress</b> — the designer is working on your request.", string.Empty),
                LeadStatus.WaitingMaterials =>
                    ("⏳ <b>Waiting for materials</b>", BuildMissingHintEn(lead)),
                LeadStatus.Converted =>
                    ("🟢 <b>Project created</b> — your request has been converted to an active project.", string.Empty),
                LeadStatus.Closed =>
                    ("⚪ <b>Closed</b> — request completed.", string.Empty),
                _ =>
                    ("❓ Unknown status.", string.Empty)
            };
        }
    }

    private static string BuildMissingHintUk(Domain.Entities.Lead lead)
    {
        var missing = new System.Text.StringBuilder();
        if (string.IsNullOrWhiteSpace(lead.Brand)) missing.AppendLine("• Назва бренду / компанії");
        if (string.IsNullOrWhiteSpace(lead.Audience)) missing.AppendLine("• Цільова аудиторія");
        if (string.IsNullOrWhiteSpace(lead.Style)) missing.AppendLine("• Бажаний стиль");
        if (string.IsNullOrWhiteSpace(lead.Deadline)) missing.AppendLine("• Дедлайн");
        if (string.IsNullOrWhiteSpace(lead.Budget)) missing.AppendLine("• Бюджет");

        return missing.Length > 0
            ? $"\n\nДля продовження роботи потрібна наступна інформація:\n{missing.ToString().TrimEnd()}"
            : "\n\nЗв'яжіться з дизайнером для уточнення деталей.";
    }

    private static string BuildMissingHintEn(Domain.Entities.Lead lead)
    {
        var missing = new System.Text.StringBuilder();
        if (string.IsNullOrWhiteSpace(lead.Brand)) missing.AppendLine("• Brand / company name");
        if (string.IsNullOrWhiteSpace(lead.Audience)) missing.AppendLine("• Target audience");
        if (string.IsNullOrWhiteSpace(lead.Style)) missing.AppendLine("• Desired style");
        if (string.IsNullOrWhiteSpace(lead.Deadline)) missing.AppendLine("• Deadline");
        if (string.IsNullOrWhiteSpace(lead.Budget)) missing.AppendLine("• Budget");

        return missing.Length > 0
            ? $"\n\nTo continue, we need the following information:\n{missing.ToString().TrimEnd()}"
            : "\n\nContact the designer to clarify the details.";
    }

    private static object Btn(string text, string callbackData)
        => new { text, callback_data = callbackData };

    private static string ResolveLocale(string? languageCode)
        => languageCode?.StartsWith("uk", StringComparison.OrdinalIgnoreCase) == true ? "uk" : "en";
}
