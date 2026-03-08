namespace LagerthaAssistant.Domain.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

