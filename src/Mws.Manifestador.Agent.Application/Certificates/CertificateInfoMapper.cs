using Mws.Manifestador.Agent.Application.DTOs;

namespace Mws.Manifestador.Agent.Application.Certificates;

public static class CertificateInfoMapper
{
    public static CertificateInfo ToCertificateInfo(CertificateSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new CertificateInfo(
            summary.Thumbprint,
            summary.Subject,
            summary.Issuer,
            summary.SerialNumber,
            summary.NotBefore,
            summary.NotAfter,
            summary.HasPrivateKey);
    }
}
