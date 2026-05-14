namespace Mws.Manifestador.Agent.Application.DTOs;

public sealed record ActivationResponse(
    Guid AgentId,
    string Secret,
    int PollingIntervalSeconds,
    int TimestampToleranceSeconds);
