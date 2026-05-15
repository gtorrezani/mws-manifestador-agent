using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Mws.Manifestador.Agent.Application.Certificates;
using Mws.Manifestador.Agent.Application.Interfaces;

namespace Mws.Manifestador.Agent.Sefaz.Certificates;

public sealed class WindowsCertificateProvider : ICertificateProvider
{
    private const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";
    private const string IcpBrasilOidPrefix = "2.16.76.1.";

    private static readonly Regex CnpjRegex = new(@"(?<!\d)(\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2})(?!\d)", RegexOptions.Compiled | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
    private static readonly Regex CpfRegex = new(@"(?<!\d)(\d{3}\.?\d{3}\.?\d{3}-?\d{2})(?!\d)", RegexOptions.Compiled | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

    private static readonly string[] IcpBrasilKeywords =
    [
        "ICP-Brasil",
        "AC SOLUTI",
        "Receita Federal",
        "RFB",
        "Serasa",
        "Certisign",
        "Valid",
        "Safeweb",
        "SERPRO",
        "Fenacon",
        "DigitalSign",
        "PRODEMGE",
        "Caixa Economica Federal",
        "Autoridade Certificadora",
    ];

    private static readonly string[] SystemCertificateKeywords =
    [
        "Microsoft",
        "Windows Admin Center",
        "WindowsAdminCenter",
        "Windows Admin",
        "localhost",
        "Self-Signed",
        "Remote Desktop",
    ];

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
            .OrderByDescending(static certificate => certificate.IsFiscalCandidate)
            .ThenBy(static certificate => certificate.Subject, StringComparer.Ordinal)
            .ToArray());
    }

    public async Task<X509Certificate2> GetCertificateAsync(CertificateReference reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        cancellationToken.ThrowIfCancellationRequested();

        if (reference.Kind != CertificateKind.A3)
        {
            throw new NotSupportedException("Only fiscal certificates from Windows Certificate Store are supported by this provider. A1 PFX support must use a separate provider and protected secret.");
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

        if (summary is null || !summary.IsFiscalCandidate)
        {
            certificate.Dispose();
            throw new CertificateProviderException(CertificateErrorCode.CertificateInvalid, summary?.RejectionReasons?.FirstOrDefault() ?? "Certificate is not a usable ICP-Brasil fiscal certificate.");
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
        string searchableText = SearchableCertificateText(certificate);
        (string? document, string? documentType) = ExtractDocument(searchableText);
        string? cnpj = string.Equals(documentType, "cnpj", StringComparison.Ordinal) ? document : null;
        bool isExpired = new DateTimeOffset(certificate.NotAfter) < DateTimeOffset.UtcNow;
        bool notYetValid = new DateTimeOffset(certificate.NotBefore) > DateTimeOffset.UtcNow;
        bool isCertificateAuthority = IsCertificateAuthority(certificate);
        bool isIcpBrasil = IsIcpBrasil(certificate, searchableText);
        bool isUsableForClientAuth = IsUsableForClientAuth(certificate);
        bool isSystemCertificate = ContainsAny(searchableText, SystemCertificateKeywords) && !isIcpBrasil;
        List<string> rejectionReasons = GetRejectionReasons(certificate, isExpired, notYetValid, isCertificateAuthority, isIcpBrasil, isUsableForClientAuth, document);
        bool isFiscalCandidate = rejectionReasons.Count == 0;
        string classification = Classification(isFiscalCandidate, isExpired, isCertificateAuthority, certificate.HasPrivateKey, isSystemCertificate, isIcpBrasil, document);
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
            scope,
            CommonName(certificate),
            document,
            documentType,
            isCertificateAuthority,
            isIcpBrasil,
            isUsableForClientAuth,
            isFiscalCandidate,
            classification,
            rejectionReasons,
            isFiscalCandidate ? ["Tipo A1/A3 nao confirmado automaticamente."] : []);
    }

    private static List<string> GetRejectionReasons(
        X509Certificate2 certificate,
        bool isExpired,
        bool notYetValid,
        bool isCertificateAuthority,
        bool isIcpBrasil,
        bool isUsableForClientAuth,
        string? document)
    {
        List<string> rejectionReasons = [];
        AddRejectionReason(rejectionReasons, !certificate.HasPrivateKey, "Certificado sem chave privada.");
        AddRejectionReason(rejectionReasons, isExpired, "Certificado vencido.");
        AddRejectionReason(rejectionReasons, notYetValid, "Certificado ainda nao esta valido.");
        AddRejectionReason(rejectionReasons, isCertificateAuthority, "Certificado de autoridade certificadora.");
        AddRejectionReason(rejectionReasons, !isIcpBrasil, "Emissor/cadeia nao indica ICP-Brasil.");
        AddRejectionReason(rejectionReasons, document is null, "CPF/CNPJ nao identificado no certificado.");
        AddRejectionReason(rejectionReasons, !isUsableForClientAuth, "Uso do certificado nao e compativel com autenticacao/assinatura de cliente.");

        return rejectionReasons;
    }

    private static void AddRejectionReason(List<string> rejectionReasons, bool condition, string message)
    {
        if (condition)
        {
            rejectionReasons.Add(message);
        }
    }

    private static string Classification(
        bool isFiscalCandidate,
        bool isExpired,
        bool isCertificateAuthority,
        bool hasPrivateKey,
        bool isSystemCertificate,
        bool isIcpBrasil,
        string? document)
    {
        if (isFiscalCandidate)
        {
            return "fiscal_candidate";
        }

        if (isExpired && (isIcpBrasil || document is not null))
        {
            return "expired_fiscal";
        }

        if (isCertificateAuthority)
        {
            return "ca_certificate";
        }

        if (!hasPrivateKey)
        {
            return "missing_private_key";
        }

        if (isSystemCertificate)
        {
            return "system_certificate";
        }

        return "unknown";
    }

    private static bool IsCertificateAuthority(X509Certificate2 certificate)
    {
        return certificate.Extensions
            .OfType<X509BasicConstraintsExtension>()
            .Any(static extension => extension.CertificateAuthority);
    }

    private static bool IsUsableForClientAuth(X509Certificate2 certificate)
    {
        X509EnhancedKeyUsageExtension? enhancedKeyUsage = certificate.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .FirstOrDefault();

        bool clientAuthAllowed = enhancedKeyUsage is null ||
            enhancedKeyUsage.EnhancedKeyUsages
                .Cast<Oid>()
                .Any(static oid => string.Equals(oid.Value, ClientAuthenticationOid, StringComparison.Ordinal));

        X509KeyUsageExtension? keyUsage = certificate.Extensions
            .OfType<X509KeyUsageExtension>()
            .FirstOrDefault();

        bool signatureAllowed = keyUsage is null ||
            (keyUsage.KeyUsages & (X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation)) != X509KeyUsageFlags.None;

        return clientAuthAllowed && signatureAllowed;
    }

    private static bool IsIcpBrasil(X509Certificate2 certificate, string searchableText)
    {
        return ContainsAny(searchableText, IcpBrasilKeywords) ||
            certificate.Extensions.Any(static extension => extension.Oid?.Value?.StartsWith(IcpBrasilOidPrefix, StringComparison.Ordinal) == true);
    }

    private static string SearchableCertificateText(X509Certificate2 certificate)
    {
        List<string> parts = [certificate.Subject, certificate.Issuer, CommonName(certificate) ?? string.Empty];

        foreach (X509Extension extension in certificate.Extensions)
        {
            if (extension.Oid?.Value is not null)
            {
                parts.Add(extension.Oid.Value);
            }

            if (extension.Oid?.FriendlyName is not null)
            {
                parts.Add(extension.Oid.FriendlyName);
            }

            try
            {
                parts.Add(extension.Format(multiLine: true));
            }
            catch (CryptographicException)
            {
                // Some provider extensions cannot be formatted outside their token context.
            }
        }

        return string.Join(' ', parts);
    }

    private static string? CommonName(X509Certificate2 certificate)
    {
        string commonName = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false).Trim();
        if (!string.IsNullOrWhiteSpace(commonName))
        {
            return commonName;
        }

        const string prefix = "CN=";
        string? subjectPart = certificate.Subject
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(part => part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        return subjectPart is null ? null : subjectPart[prefix.Length..].Trim();
    }

    private static (string? Document, string? DocumentType) ExtractDocument(string value)
    {
        Match cnpj = CnpjRegex.Match(value);
        if (cnpj.Success)
        {
            return (Digits(cnpj.Value), "cnpj");
        }

        Match cpf = CpfRegex.Match(value);
        if (cpf.Success)
        {
            return (Digits(cpf.Value), "cpf");
        }

        return (null, null);
    }

    private static bool ContainsAny(string value, IEnumerable<string> needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static string Digits(string value)
    {
        return Regex.Replace(value, @"\D", string.Empty, RegexOptions.None, TimeSpan.FromSeconds(1));
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
