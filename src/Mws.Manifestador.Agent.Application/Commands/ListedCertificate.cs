namespace Mws.Manifestador.Agent.Application.Commands;

public sealed record ListedCertificate(
    string Subject,
    string Issuer,
    string Thumbprint,
    string SerialNumber,
    string NotBefore,
    string NotAfter,
    bool HasPrivateKey,
    string StoreLocation,
    string? Cnpj,
    bool IsExpired,
    bool IsValid,
    string? ValidationMessage);
