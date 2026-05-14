using Mws.Manifestador.Agent.Application.DTOs;

namespace Mws.Manifestador.Agent.Application.Interfaces;

public interface IAgentCredentialStore
{
    Task<AgentCredentials?> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(AgentCredentials credentials, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
