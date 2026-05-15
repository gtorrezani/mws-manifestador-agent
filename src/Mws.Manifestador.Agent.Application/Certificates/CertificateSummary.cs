namespace Mws.Manifestador.Agent.Application.Certificates;

public sealed record CertificateSummary(
    CertificateReference Reference,
    string Subject,
    string Issuer,
    string Thumbprint,
    string SerialNumber,
    DateTimeOffset NotBefore,
    DateTimeOffset NotAfter,
    bool HasPrivateKey,
    string? Cnpj,
    CertificateStoreScope StoreScope,
    string? CommonName = null,
    string? Document = null,
    string? DocumentType = null,
    bool IsCertificateAuthority = false,
    bool IsIcpBrasil = false,
    bool IsUsableForClientAuth = false,
    bool IsFiscalCandidate = false,
    string Classification = "unknown",
    IReadOnlyCollection<string>? RejectionReasons = null,
    IReadOnlyCollection<string>? Warnings = null);
