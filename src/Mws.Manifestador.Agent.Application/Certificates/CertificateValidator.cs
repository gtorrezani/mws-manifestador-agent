using Mws.Manifestador.Agent.Application.Interfaces;

namespace Mws.Manifestador.Agent.Application.Certificates;

public sealed class CertificateValidator : ICertificateValidator
{
    private readonly ICertificateProvider provider;

    public CertificateValidator(ICertificateProvider provider)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public async Task<CertificateValidationResult> ValidateAsync(CertificateReference reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);

        try
        {
            IReadOnlyCollection<CertificateSummary> certificates = await provider.ListAsync(cancellationToken).ConfigureAwait(false);
            CertificateSummary? summary = certificates.FirstOrDefault(certificate =>
                string.Equals(certificate.Thumbprint, reference.Thumbprint, StringComparison.OrdinalIgnoreCase) &&
                (reference.StoreScope is null || certificate.StoreScope == reference.StoreScope));

            if (summary is null)
            {
                return CertificateValidationResult.Invalid(CertificateErrorCode.CertificateNotFound, "Certificate was not found.");
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (summary.NotAfter < now)
            {
                return CertificateValidationResult.Invalid(CertificateErrorCode.CertificateExpired, "Certificate is expired.", summary);
            }

            if (summary.NotBefore > now)
            {
                return CertificateValidationResult.Invalid(CertificateErrorCode.CertificateInvalid, "Certificate is not valid yet.", summary);
            }

            if (!summary.HasPrivateKey)
            {
                return CertificateValidationResult.Invalid(CertificateErrorCode.CertificateWithoutPrivateKey, "Certificate does not have a private key.", summary);
            }

            return CertificateValidationResult.Valid(summary);
        }
        catch (UnauthorizedAccessException exception)
        {
            return CertificateValidationResult.Invalid(CertificateErrorCode.CertificateProviderAccessDenied, exception.Message);
        }
        catch (System.Security.Cryptography.CryptographicException exception)
        {
            return CertificateValidationResult.Invalid(ClassifyCryptographicException(exception), exception.Message);
        }
    }

    private static CertificateErrorCode ClassifyCryptographicException(System.Security.Cryptography.CryptographicException exception)
    {
        string message = exception.Message;
        if (message.Contains("cancel", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("cancelado", StringComparison.OrdinalIgnoreCase))
        {
            return CertificateErrorCode.CertificatePinCancelled;
        }

        if (message.Contains("keyset", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("smart card", StringComparison.OrdinalIgnoreCase))
        {
            return CertificateErrorCode.CertificateTokenUnavailable;
        }

        return CertificateErrorCode.CertificateProviderAccessDenied;
    }
}
