namespace Mws.Manifestador.Agent.Application.DTOs;

public sealed record ActivationRequest(
    string ActivationCode,
    string InstallationId,
    string MachineName,
    string Version,
    IReadOnlyCollection<CertificateInfo> CertificateInventory);
