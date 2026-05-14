using Mws.Manifestador.Agent.Application.Certificates;

namespace Mws.Manifestador.Agent.Sefaz.Certificates;

public sealed class CertificateProviderException : Exception
{
    public CertificateProviderException()
    {
    }

    public CertificateProviderException(string message)
        : base(message)
    {
    }

    public CertificateProviderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public CertificateProviderException(CertificateErrorCode errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public CertificateProviderException(CertificateErrorCode errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

    public CertificateErrorCode ErrorCode { get; }
}
