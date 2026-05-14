using Mws.Manifestador.Agent.Domain.Enums;

namespace Mws.Manifestador.Agent.Application.DTOs;

public sealed record HeartbeatRequest(
    AgentStatus Status,
    string Version,
    string MachineName,
    object Metrics,
    IReadOnlyCollection<CertificateInfo> CertificateInventory);
