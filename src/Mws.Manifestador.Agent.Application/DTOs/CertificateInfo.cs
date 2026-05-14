namespace Mws.Manifestador.Agent.Application.DTOs;

public sealed record CertificateInfo(
    string Thumbprint,
    string SubjectName,
    string IssuerName,
    string SerialNumber,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    bool HasPrivateKey);
