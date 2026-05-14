using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Domain.Entities;
using Mws.Manifestador.Agent.Domain.Enums;

namespace Mws.Manifestador.Agent.Application.Interfaces;

public interface ICommandHandler
{
    CommandType Type { get; }

    Task<CommandExecutionOutcome> ExecuteAsync(AgentCommand command, CancellationToken cancellationToken);
}
