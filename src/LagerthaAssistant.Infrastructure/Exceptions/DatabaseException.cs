namespace LagerthaAssistant.Infrastructure.Exceptions;

public sealed class DatabaseException : InfrastructureException
{
    public DatabaseException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

