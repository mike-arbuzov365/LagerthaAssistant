namespace BaguetteDesign.Infrastructure.Workers;

using BaguetteDesign.Application.Interfaces;
using BaguetteDesign.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public sealed class ReminderWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderWorker> _logger;

    public ReminderWorker(IServiceScopeFactory scopeFactory, ILogger<ReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReminderWorker: error processing reminders");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessDueRemindersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var sender = scope.ServiceProvider.GetRequiredService<SharedBotKernel.Infrastructure.Telegram.ITelegramBotSender>();

        var due = await notifications.GetDueAsync(DateTimeOffset.UtcNow, ct);

        foreach (var notification in due)
        {
            try
            {
                await SendReminderAsync(sender, notification, ct);
                await notifications.MarkSentAsync(notification.Id, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send reminder {Id}", notification.Id);
            }
        }

        if (due.Count > 0)
            await notifications.SaveChangesAsync(ct);
    }

    private static async Task SendReminderAsync(
        SharedBotKernel.Infrastructure.Telegram.ITelegramBotSender sender,
        Domain.Entities.Notification notification,
        CancellationToken ct)
    {
        if (!long.TryParse(notification.UserId, out var chatId))
            return;

        var message = notification.Trigger switch
        {
            NotificationTrigger.CalendarReminder24h => BuildCalendarReminder(notification, hoursAhead: 24),
            NotificationTrigger.CalendarReminder1h => BuildCalendarReminder(notification, hoursAhead: 1),
            NotificationTrigger.ClientNoReply3Days =>
                "⏰ Нагадування: клієнт не відповідав 3 дні. Перевірте inbox.",
            NotificationTrigger.DeadlineTomorrow =>
                "⏰ Нагадування: дедлайн завтра! Перевірте проєкти.",
            _ => null
        };

        if (message is not null)
            await sender.SendTextAsync(chatId, message, cancellationToken: ct);
    }

    private static string BuildCalendarReminder(Domain.Entities.Notification notification, int hoursAhead)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<CalendarPayload>(notification.Payload, JsonOpts);
            if (payload is not null)
            {
                var localTime = payload.StartUtc.ToLocalTime();
                return hoursAhead == 24
                    ? $"🔔 Нагадування: дзвінок завтра о {localTime:HH:mm} ({localTime:dd.MM})."
                    : $"🔔 Нагадування: дзвінок через 1 годину о {localTime:HH:mm}.";
            }
        }
        catch { /* ignore malformed payload */ }

        return hoursAhead == 24
            ? "🔔 Нагадування: у вас дзвінок завтра."
            : "🔔 Нагадування: у вас дзвінок через 1 годину.";
    }

    private sealed record CalendarPayload(DateTimeOffset StartUtc, DateTimeOffset EndUtc);
}
