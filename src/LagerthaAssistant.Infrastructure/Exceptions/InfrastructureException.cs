namespace LagerthaAssistant.Infrastructure.Exceptions;

public abstract class InfrastructureException : Exception
{
    protected InfrastructureException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

