namespace Mws.Manifestador.Agent.Infrastructure.LocalStatus;

public sealed record AgentLocalStatusUpdate
{
    public string? AgentId { get; init; }

    public string? InstallationId { get; init; }

    public Uri? ApiBaseUrl { get; init; }

    public bool? Activated { get; init; }

    public DateTimeOffset? LastHeartbeatAt { get; init; }

    public DateTimeOffset? LastPollAt { get; init; }

    public string? Version { get; init; }

    public string? LastErrorMessage { get; init; }
}
