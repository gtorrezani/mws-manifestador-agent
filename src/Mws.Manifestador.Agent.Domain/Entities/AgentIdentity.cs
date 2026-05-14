namespace Mws.Manifestador.Agent.Domain.Entities;

public sealed record AgentIdentity(
    Guid AgentId,
    string InstallationId,
    string MachineName,
    string Version);
