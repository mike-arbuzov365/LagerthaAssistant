namespace BaguetteDesign.Infrastructure.Telegram;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Models;
using BaguetteDesign.Infrastructure.Options;
using SharedBotKernel.Infrastructure.Telegram;

public sealed class DesignerNotifier : IDesignerNotifier
{
    private readonly ITelegramBotSender _sender;
    private readonly BaguetteOptions _options;

    public DesignerNotifier(ITelegramBotSender sender, BaguetteOptions options)
    {
        _sender = sender;
        _options = options;
    }

    public async Task NotifyMessageReceivedAsync(long clientUserId, string message, CancellationToken cancellationToken = default)
    {
        if (_options.AdminUserId == 0) return;

        var text = $"📩 <b>Нове повідомлення від клієнта</b>\n\n" +
                   $"👤 User ID: <code>{clientUserId}</code>\n\n" +
                   $"💬 {message}";

        await _sender.SendTextAsync(_options.AdminUserId, text,
            new TelegramSendOptions(ParseMode: "HTML"),
            cancellationToken: cancellationToken);
    }

    public async Task NotifySlotBookedAsync(long clientUserId, CalendarSlot slot, string? meetLink, CancellationToken cancellationToken = default)
    {
        if (_options.AdminUserId == 0) return;

        var text = $"📅 <b>Клієнт записався на дзвінок</b>\n\n" +
                   $"👤 User ID: <code>{clientUserId}</code>\n" +
                   $"🕐 {slot.FormatUk()}";

        if (!string.IsNullOrWhiteSpace(meetLink))
            text += $"\n🎥 <a href=\"{meetLink}\">Google Meet</a>";

        await _sender.SendTextAsync(_options.AdminUserId, text,
            new TelegramSendOptions(ParseMode: "HTML"),
            cancellationToken: cancellationToken);
    }
}
