namespace Mws.Manifestador.Agent.Domain.Exceptions;

public sealed class InvalidAccessKeyException : DomainException
{
    public InvalidAccessKeyException()
    {
    }

    public InvalidAccessKeyException(string message)
        : base(message)
    {
    }

    public InvalidAccessKeyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
