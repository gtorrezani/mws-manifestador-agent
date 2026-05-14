using System.Net;

namespace Mws.Manifestador.Agent.Infrastructure.Api;

public sealed class AgentApiException : Exception
{
    public AgentApiException()
        : this("Agent API request failed.")
    {
    }

    public AgentApiException(string message)
        : base(message)
    {
        SanitizedBody = string.Empty;
    }

    public AgentApiException(string message, Exception innerException)
        : base(message, innerException)
    {
        SanitizedBody = string.Empty;
    }

    public AgentApiException(
        HttpStatusCode statusCode,
        string sanitizedBody,
        string? correlationId,
        string message)
        : base(message)
    {
        StatusCode = statusCode;
        SanitizedBody = sanitizedBody;
        CorrelationId = correlationId;
    }

    public HttpStatusCode StatusCode { get; }

    public string SanitizedBody { get; }

    public string? CorrelationId { get; }
}
