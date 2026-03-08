namespace LagerthaAssistant.Infrastructure.Exceptions;

public sealed class RepositoryException : InfrastructureException
{
    public string RepositoryName { get; }
    public string Operation { get; }

    public RepositoryException(string repositoryName, string operation, string message, Exception innerException)
        : base($"{repositoryName}.{operation}: {message}", innerException)
    {
        RepositoryName = repositoryName;
        Operation = operation;
    }
}

