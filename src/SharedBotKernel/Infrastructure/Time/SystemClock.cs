namespace SharedBotKernel.Infrastructure.Time;

using SharedBotKernel.Domain.Abstractions;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
