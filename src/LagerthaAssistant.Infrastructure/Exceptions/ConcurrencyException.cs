namespace LagerthaAssistant.Infrastructure.Exceptions;

public sealed class ConcurrencyException : InfrastructureException
{
    public ConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

