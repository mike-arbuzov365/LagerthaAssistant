namespace LagerthaAssistant.Infrastructure.Time;

using LagerthaAssistant.Domain.Abstractions;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

