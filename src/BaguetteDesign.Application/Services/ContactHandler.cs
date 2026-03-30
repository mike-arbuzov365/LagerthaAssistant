namespace BaguetteDesign.Application.Services;

using System.Text.Json;
using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Entities;
using BaguetteDesign.Domain.Enums;
using BaguetteDesign.Domain.Models;
using SharedBotKernel.Infrastructure.Telegram;

public sealed class ContactHandler : IContactHandler
{
    // Slot keys are stored in UserMemory as JSON to survive across messages
    private const string PendingSlotsKey = "contact_pending_slots";
    private const string AwaitingMessageKey = "contact_awaiting_message";
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly ICalendarService _calendar;
    private readonly ICalendarEventRepository _calendarEvents;
    private readonly INotificationRepository _notifications;
    private readonly IDesignerNotifier _designerNotifier;
    private readonly IUserMemoryRepository _memory;
    private readonly ITelegramBotSender _sender;

    public ContactHandler(
        ICalendarService calendar,
        ICalendarEventRepository calendarEvents,
        INotificationRepository notifications,
        IDesignerNotifier designerNotifier,
        IUserMemoryRepository memory,
        ITelegramBotSender sender)
    {
        _calendar = calendar;
        _calendarEvents = calendarEvents;
        _notifications = notifications;
        _designerNotifier = designerNotifier;
        _memory = memory;
        _sender = sender;
    }

    public async Task ShowOptionsAsync(long chatId, string? languageCode, CancellationToken cancellationToken = default)
    {
        var locale = ResolveLocale(languageCode);

        var text = locale == "uk"
            ? "🔗 <b>Зв'язатися з дизайнером</b>\n\nОберіть спосіб:"
            : "🔗 <b>Contact the designer</b>\n\nChoose how:";

        var keyboard = new
        {
            inline_keyboard = new[]
            {
                new[] { Btn(locale == "uk" ? "✉️ Надіслати повідомлення" : "✉️ Send a message", "contact_message") },
                new[] { Btn(locale == "uk" ? "📋 Заповнити бриф" : "📋 Fill in brief", "brief") },
                new[] { Btn(locale == "uk" ? "📅 Записатись на дзвінок" : "📅 Book a call", "contact_call") }
            }
        };

        await _sender.SendTextAsync(chatId, text,
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: keyboard),
            cancellationToken: cancellationToken);
    }

    public async Task PromptForMessageAsync(long chatId, string? languageCode, CancellationToken cancellationToken = default)
    {
        var locale = ResolveLocale(languageCode);
        await _memory.SetAsync(chatId.ToString(), AwaitingMessageKey, "1", cancellationToken);

        var prompt = locale == "uk"
            ? "✉️ Напишіть ваше повідомлення дизайнеру. Він отримає його і відповість найближчим часом."
            : "✉️ Type your message to the designer. They'll receive it and reply shortly.";

        await _sender.SendTextAsync(chatId, prompt, cancellationToken: cancellationToken);
    }

    public async Task<bool> IsAwaitingMessageAsync(string userId, CancellationToken cancellationToken = default)
    {
        var val = await _memory.GetAsync(userId, AwaitingMessageKey, cancellationToken);
        return val is not null;
    }

    public async Task HandleSendMessageAsync(long chatId, long userId, string message, string? languageCode, CancellationToken cancellationToken = default)
    {
        var locale = ResolveLocale(languageCode);

        // Clear awaiting flag (keyed by chatId, same as PromptForMessageAsync)
        await _memory.DeleteAsync(chatId.ToString(), AwaitingMessageKey, cancellationToken);

        // Forward to designer
        await _designerNotifier.NotifyMessageReceivedAsync(userId, message, cancellationToken);

        var reply = locale == "uk"
            ? "✅ Ваше повідомлення надіслано дизайнеру. Зазвичай відповідаємо протягом декількох годин."
            : "✅ Your message has been sent to the designer. We usually reply within a few hours.";

        var keyboard = new
        {
            inline_keyboard = new[]
            {
                new[] { Btn(locale == "uk" ? "📋 Заповнити бриф" : "📋 Fill in brief", "brief"),
                        Btn(locale == "uk" ? "💰 Прайс" : "💰 Pricing", "price") }
            }
        };

        await _sender.SendTextAsync(chatId, reply,
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: keyboard),
            cancellationToken: cancellationToken);
    }

    public async Task ShowCalendarSlotsAsync(long chatId, string? languageCode, CancellationToken cancellationToken = default)
    {
        var locale = ResolveLocale(languageCode);
        var slots = await _calendar.GetAvailableSlotsAsync(daysAhead: 7, cancellationToken);

        if (slots.Count == 0)
        {
            var noSlots = locale == "uk"
                ? "😔 На жаль, вільних слотів на найближчий тиждень немає.\n\nНадішліть повідомлення дизайнеру і ми домовимось про зручний час."
                : "😔 No available slots for the next week.\n\nSend the designer a message and we'll arrange a convenient time.";

            var keyboard = new
            {
                inline_keyboard = new[]
                {
                    new[] { Btn(locale == "uk" ? "✉️ Надіслати повідомлення" : "✉️ Send a message", "contact_message") }
                }
            };

            await _sender.SendTextAsync(chatId, noSlots,
                new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: keyboard),
                cancellationToken: cancellationToken);
            return;
        }

        // Persist slots list so we can look up by index in BookSlotAsync
        var slotList = slots.ToList();
        await _memory.SetAsync(chatId.ToString(), PendingSlotsKey,
            JsonSerializer.Serialize(slotList, JsonOpts), cancellationToken);

        var title = locale == "uk"
            ? "📅 <b>Оберіть зручний час для дзвінка:</b>"
            : "📅 <b>Choose a convenient time for the call:</b>";

        var slotButtons = slotList
            .Take(8) // Telegram limits: keep keyboard manageable
            .Select((s, i) => new[]
            {
                Btn(locale == "uk" ? s.FormatUk() : s.FormatEn(), $"contact_slot_{i}")
            })
            .ToArray();

        await _sender.SendTextAsync(chatId, title,
            new TelegramSendOptions(ParseMode: "HTML", ReplyMarkup: new { inline_keyboard = slotButtons }),
            cancellationToken: cancellationToken);
    }

    public async Task BookSlotAsync(long chatId, long userId, string slotKey, string? languageCode, CancellationToken cancellationToken = default)
    {
        var locale = ResolveLocale(languageCode);

        if (!int.TryParse(slotKey, out var index))
        {
            await ShowCalendarSlotsAsync(chatId, languageCode, cancellationToken);
            return;
        }

        // Load persisted slots
        var raw = await _memory.GetAsync(chatId.ToString(), PendingSlotsKey, cancellationToken);
        if (raw is null)
        {
            await ShowCalendarSlotsAsync(chatId, languageCode, cancellationToken);
            return;
        }

        var slots = JsonSerializer.Deserialize<List<CalendarSlot>>(raw, JsonOpts) ?? [];
        if (index < 0 || index >= slots.Count)
        {
            await ShowCalendarSlotsAsync(chatId, languageCode, cancellationToken);
            return;
        }

        var slot = slots[index];
        var summary = locale == "uk"
            ? $"Дзвінок з клієнтом {userId}"
            : $"Call with client {userId}";

        var meetLink = await _calendar.BookSlotAsync(slot, userId.ToString(), summary, cancellationToken);

        // Persist calendar event
        var calEvent = new CalendarEvent
        {
            UserId = userId.ToString(),
            GoogleEventId = meetLink ?? $"local_{Guid.NewGuid():N}",
            Title = summary,
            StartUtc = slot.StartUtc,
            EndUtc = slot.EndUtc,
            MeetLink = meetLink
        };
        await _calendarEvents.AddAsync(calEvent, cancellationToken);
        await _calendarEvents.SaveChangesAsync(cancellationToken);

        // Schedule reminders
        await ScheduleReminderAsync(userId.ToString(), slot, NotificationTrigger.CalendarReminder24h,
            slot.StartUtc.AddHours(-24), cancellationToken);
        await ScheduleReminderAsync(userId.ToString(), slot, NotificationTrigger.CalendarReminder1h,
            slot.StartUtc.AddHours(-1), cancellationToken);

        // Notify designer
        await _designerNotifier.NotifySlotBookedAsync(userId, slot, meetLink, cancellationToken);

        // Clear pending slots
        await _memory.DeleteAsync(chatId.ToString(), PendingSlotsKey, cancellationToken);

        var slotLabel = locale == "uk" ? slot.FormatUk() : slot.FormatEn();
        var confirmation = locale == "uk"
            ? $"✅ <b>Дзвінок заброньовано!</b>\n\n📅 {slotLabel}\n\nДизайнер отримав сповіщення. Ми нагадаємо вам за 24 години і за 1 годину до дзвінка."
            : $"✅ <b>Call booked!</b>\n\n📅 {slotLabel}\n\nThe designer has been notified. We'll remind you 24 hours and 1 hour before the call.";

        if (!string.IsNullOrWhiteSpace(meetLink))
            confirmation += locale == "uk"
                ? $"\n\n🎥 <a href=\"{meetLink}\">Посилання на Google Meet</a>"
                : $"\n\n🎥 <a href=\"{meetLink}\">Google Meet link</a>";

        await _sender.SendTextAsync(chatId, confirmation,
            new TelegramSendOptions(ParseMode: "HTML"),
            cancellationToken: cancellationToken);
    }

    private async Task ScheduleReminderAsync(string userId, CalendarSlot slot, NotificationTrigger trigger,
        DateTimeOffset scheduledAt, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            slot.StartUtc,
            slot.EndUtc
        }, JsonOpts);

        await _notifications.AddAsync(new Notification
        {
            UserId = userId,
            Trigger = trigger,
            Payload = payload,
            ScheduledAtUtc = scheduledAt,
            IsSent = false
        }, ct);
        await _notifications.SaveChangesAsync(ct);
    }

    private static object Btn(string text, string callbackData)
        => new { text, callback_data = callbackData };

    private static string ResolveLocale(string? languageCode)
        => languageCode?.StartsWith("uk", StringComparison.OrdinalIgnoreCase) == true ? "uk" : "en";
}
