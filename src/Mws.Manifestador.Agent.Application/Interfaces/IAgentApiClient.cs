using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Domain.Entities;

namespace Mws.Manifestador.Agent.Application.Interfaces;

public interface IAgentApiClient
{
    Task<ActivationResponse> ActivateAsync(ActivationRequest request, CancellationToken cancellationToken);

    Task SendHeartbeatAsync(AgentCredentials credentials, HeartbeatRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AgentCommand>> PollCommandsAsync(AgentCredentials credentials, PollCommandsRequest request, CancellationToken cancellationToken);

    Task StartCommandAsync(AgentCredentials credentials, Guid commandId, CancellationToken cancellationToken);

    Task CompleteCommandAsync(AgentCredentials credentials, Guid commandId, CommandExecutionResult result, CancellationToken cancellationToken);

    Task FailCommandAsync(AgentCredentials credentials, Guid commandId, CommandExecutionFailure failure, CancellationToken cancellationToken);
}
