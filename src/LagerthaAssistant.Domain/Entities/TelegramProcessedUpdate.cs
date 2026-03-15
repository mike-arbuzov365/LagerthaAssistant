namespace LagerthaAssistant.Domain.Entities;

public sealed class TelegramProcessedUpdate
{
    public long UpdateId { get; set; }

    public DateTimeOffset ProcessedAtUtc { get; set; }
}
