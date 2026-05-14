namespace Mws.Manifestador.Agent.Application.Certificates;

public enum CertificateErrorCode
{
    None = 0,
    CertificateNotFound,
    CertificateExpired,
    CertificateWithoutPrivateKey,
    CertificateProviderAccessDenied,
    CertificatePinCancelled,
    CertificateTokenUnavailable,
    CertificateStoreUnavailable,
    CertificateInvalid,
}
