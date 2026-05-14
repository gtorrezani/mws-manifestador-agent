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
    CertificateStoreScope StoreScope);
