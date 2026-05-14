using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Domain.Enums;

namespace Mws.Manifestador.Agent.Application.Services;

public sealed class HeartbeatService
{
    private readonly IAgentApiClient apiClient;
    private readonly IAgentEnvironment environment;
    private readonly ICertificateStore certificateStore;

    public HeartbeatService(
        IAgentApiClient apiClient,
        IAgentEnvironment environment,
        ICertificateStore certificateStore)
    {
        this.apiClient = apiClient;
        this.environment = environment;
        this.certificateStore = certificateStore;
    }

    public async Task SendAsync(AgentCredentials credentials, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<CertificateInfo> certificates = await certificateStore.ListAsync(cancellationToken).ConfigureAwait(false);
        HeartbeatRequest request = new(
            AgentStatus.Online,
            environment.Version,
            environment.MachineName,
            new
            {
                utc_time = DateTimeOffset.UtcNow,
            },
            certificates);

        await apiClient.SendHeartbeatAsync(credentials, request, cancellationToken).ConfigureAwait(false);
    }
}
