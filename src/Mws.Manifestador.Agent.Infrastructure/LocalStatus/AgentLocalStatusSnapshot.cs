using System.Text.Json.Serialization;

namespace Mws.Manifestador.Agent.Infrastructure.LocalStatus;

public sealed record AgentLocalStatusSnapshot(
    [property: JsonPropertyName("agent_id")] string? AgentId,
    [property: JsonPropertyName("installation_id")] string? InstallationId,
    [property: JsonPropertyName("api_base_url")] Uri? ApiBaseUrl,
    [property: JsonPropertyName("activated")] bool Activated,
    [property: JsonPropertyName("last_heartbeat_at")] DateTimeOffset? LastHeartbeatAt,
    [property: JsonPropertyName("last_poll_at")] DateTimeOffset? LastPollAt,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("service_status")] string ServiceStatus,
    [property: JsonPropertyName("last_error_message")] string? LastErrorMessage)
{
    public static AgentLocalStatusSnapshot Empty { get; } = new(
        null,
        null,
        null,
        false,
        null,
        null,
        null,
        AgentServiceState.Unknown.ToString(),
        null);
}
