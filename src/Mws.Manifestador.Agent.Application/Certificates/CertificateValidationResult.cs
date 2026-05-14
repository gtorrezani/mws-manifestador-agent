namespace Mws.Manifestador.Agent.Application.Certificates;

public sealed record CertificateValidationResult(
    bool IsValid,
    CertificateErrorCode ErrorCode,
    string? Message,
    CertificateSummary? Certificate)
{
    public static CertificateValidationResult Valid(CertificateSummary certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        return new CertificateValidationResult(true, CertificateErrorCode.None, null, certificate);
    }

    public static CertificateValidationResult Invalid(CertificateErrorCode errorCode, string message, CertificateSummary? certificate = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new CertificateValidationResult(false, errorCode, message, certificate);
    }
}
