namespace Mws.Manifestador.Agent.Application.Certificates;

public sealed record CertificateSecret(
    CertificateKind Kind,
    string ProtectedPayload,
    string ProtectionProvider,
    DateTimeOffset CreatedAt);
