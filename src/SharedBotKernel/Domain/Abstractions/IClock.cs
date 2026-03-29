namespace SharedBotKernel.Domain.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
