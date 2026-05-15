using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Mws.Manifestador.Agent.Application.Certificates;
using Mws.Manifestador.Agent.Application.Interfaces;

namespace Mws.Manifestador.Agent.Sefaz.Certificates;

public sealed class WindowsCertificateProvider : ICertificateProvider
{
    private static readonly Regex CnpjRegex = new(@"(?<!\d)\d{14}(?!\d)", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static readonly Action<ILogger, CertificateStoreScope, string, Exception?> LogStoreOpenFailed =
        LoggerMessage.Define<CertificateStoreScope, string>(LogLevel.Warning, new EventId(4000, nameof(LogStoreOpenFailed)), "Failed to open certificate store {StoreScope}: {ErrorMessage}");

    private readonly ILogger<WindowsCertificateProvider> logger;

    public WindowsCertificateProvider(ILogger<WindowsCertificateProvider> logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IReadOnlyCollection<CertificateSummary>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<CertificateSummary> certificates = [];
        AddCertificates(certificates, CertificateStoreScope.CurrentUser, cancellationToken);
        AddCertificates(certificates, CertificateStoreScope.LocalMachine, cancellationToken);

        return Task.FromResult<IReadOnlyCollection<CertificateSummary>>(certificates
            .GroupBy(static certificate => new { certificate.Thumbprint, certificate.StoreScope })
            .Select(static group => group.First())
            .OrderBy(static certificate => certificate.Subject, StringComparer.Ordinal)
            .ToArray());
    }

    public async Task<X509Certificate2> GetCertificateAsync(CertificateReference reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();

        if (reference.Kind != CertificateKind.A3)
        {
            throw new NotSupportedException("Only A3 certificates from Windows Certificate Store are supported by this provider. A1 PFX support must use a separate provider and protected secret.");
        }

        X509Certificate2? certificate = reference.StoreScope switch
        {
            CertificateStoreScope.CurrentUser => FindCertificate(StoreLocation.CurrentUser, reference.Thumbprint),
            CertificateStoreScope.LocalMachine => FindCertificate(StoreLocation.LocalMachine, reference.Thumbprint),
            _ => FindCertificate(StoreLocation.CurrentUser, reference.Thumbprint)
                ?? FindCertificate(StoreLocation.LocalMachine, reference.Thumbprint),
        };

        if (certificate is null)
        {
            throw new CertificateProviderException(CertificateErrorCode.CertificateNotFound, "Certificate was not found or token was removed.");
        }

        if (!certificate.HasPrivateKey)
        {
            certificate.Dispose();
            throw new CertificateProviderException(CertificateErrorCode.CertificateWithoutPrivateKey, "Certificate does not have a private key.");
        }

        CertificateSummary? summary = (await ListAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(item =>
                string.Equals(item.Thumbprint, reference.Thumbprint, StringComparison.OrdinalIgnoreCase) &&
                (reference.StoreScope is null || item.StoreScope == reference.StoreScope));

        if (summary?.NotAfter < DateTimeOffset.UtcNow)
        {
            certificate.Dispose();
            throw new CertificateProviderException(CertificateErrorCode.CertificateExpired, "Certificate is expired.");
        }

        ValidatePrivateKeyAccess(certificate);
        return certificate;
    }

    private void AddCertificates(List<CertificateSummary> certificates, CertificateStoreScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using X509Store store = new(StoreName.My, ToStoreLocation(scope));
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

            foreach (X509Certificate2 certificate in store.Certificates)
            {
                certificates.Add(ToSummary(certificate, scope));
            }
        }
        catch (CryptographicException exception)
        {
            LogStoreOpenFailed(logger, scope, exception.Message, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            LogStoreOpenFailed(logger, scope, exception.Message, exception);
        }
    }

    private static X509Certificate2? FindCertificate(StoreLocation location, string thumbprint)
    {
        try
        {
            using X509Store store = new(StoreName.My, location);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

            X509Certificate2Collection matches = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                CertificateReference.NormalizeThumbprint(thumbprint),
                validOnly: false);

            return matches.Count == 0 ? null : matches[0];
        }
        catch (CryptographicException exception)
        {
            throw new CertificateProviderException(CertificateErrorCode.CertificateStoreUnavailable, "Unable to access the Windows Certificate Store.", exception);
        }
    }

    private static CertificateSummary ToSummary(X509Certificate2 certificate, CertificateStoreScope scope)
    {
        string thumbprint = CertificateReference.NormalizeThumbprint(certificate.Thumbprint);
        string? cnpj = ExtractCnpj(certificate);
        CertificateReference reference = CertificateReference.A3(thumbprint, scope, cnpj);

        return new CertificateSummary(
            reference,
            certificate.Subject,
            certificate.Issuer,
            thumbprint,
            certificate.SerialNumber,
            new DateTimeOffset(certificate.NotBefore),
            new DateTimeOffset(certificate.NotAfter),
            certificate.HasPrivateKey,
            cnpj,
            scope);
    }

    private static string? ExtractCnpj(X509Certificate2 certificate)
    {
        string? subjectCnpj = ExtractCnpj(certificate.Subject);
        if (subjectCnpj is not null)
        {
            return subjectCnpj;
        }

        foreach (X509Extension extension in certificate.Extensions)
        {
            string formatted = extension.Format(multiLine: true);
            string? cnpj = ExtractCnpj(formatted);
            if (cnpj is not null)
            {
                return cnpj;
            }
        }

        return null;
    }

    private static string? ExtractCnpj(string value)
    {
        Match match = CnpjRegex.Match(value);
        return match.Success ? match.Value : null;
    }

    private static StoreLocation ToStoreLocation(CertificateStoreScope scope)
    {
        return scope switch
        {
            CertificateStoreScope.CurrentUser => StoreLocation.CurrentUser,
            CertificateStoreScope.LocalMachine => StoreLocation.LocalMachine,
            _ => throw new InvalidOperationException($"Unsupported certificate store scope '{scope}'."),
        };
    }

    private static void ValidatePrivateKeyAccess(X509Certificate2 certificate)
    {
        try
        {
            byte[] payload = "mws-manifestador-certificate-test"u8.ToArray();
            using RSA? rsa = certificate.GetRSAPrivateKey();
            if (rsa is not null)
            {
                byte[] signature = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                if (!rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                {
                    throw new CertificateProviderException(CertificateErrorCode.CertificateProviderAccessDenied, "RSA private key signature verification failed.");
                }

                return;
            }

            using ECDsa? ecdsa = certificate.GetECDsaPrivateKey();
            if (ecdsa is not null)
            {
                byte[] signature = ecdsa.SignData(payload, HashAlgorithmName.SHA256);
                if (!ecdsa.VerifyData(payload, signature, HashAlgorithmName.SHA256))
                {
                    throw new CertificateProviderException(CertificateErrorCode.CertificateProviderAccessDenied, "ECDSA private key signature verification failed.");
                }

                return;
            }

            throw new CertificateProviderException(CertificateErrorCode.CertificateProviderAccessDenied, "Private key provider is not accessible.");
        }
        catch (CryptographicException exception)
        {
            throw new CertificateProviderException(ClassifyCryptographicException(exception), exception.Message, exception);
        }
    }

    private static CertificateErrorCode ClassifyCryptographicException(CryptographicException exception)
    {
        string message = exception.Message;
        if (message.Contains("cancel", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("cancelado", StringComparison.OrdinalIgnoreCase))
        {
            return CertificateErrorCode.CertificatePinCancelled;
        }

        if (message.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("smart card", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("keyset", StringComparison.OrdinalIgnoreCase))
        {
            return CertificateErrorCode.CertificateTokenUnavailable;
        }

        return CertificateErrorCode.CertificateProviderAccessDenied;
    }
}
