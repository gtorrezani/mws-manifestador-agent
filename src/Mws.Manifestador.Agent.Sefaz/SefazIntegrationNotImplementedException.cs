namespace Mws.Manifestador.Agent.Sefaz;

public sealed class SefazIntegrationNotImplementedException : NotSupportedException
{
    public SefazIntegrationNotImplementedException()
    {
    }

    public SefazIntegrationNotImplementedException(string message)
        : base(message)
    {
    }

    public SefazIntegrationNotImplementedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
