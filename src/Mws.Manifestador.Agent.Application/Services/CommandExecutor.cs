using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Domain.Entities;

namespace Mws.Manifestador.Agent.Application.Services;

public sealed class CommandExecutor
{
    private readonly Dictionary<Domain.Enums.CommandType, ICommandHandler> handlers;

    public CommandExecutor(IEnumerable<ICommandHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        this.handlers = handlers.ToDictionary(static handler => handler.Type);
    }

    public async Task<CommandExecutionOutcome> ExecuteAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!handlers.TryGetValue(command.Type, out ICommandHandler? handler))
        {
            return CommandExecutionOutcome.FromFailure(new CommandExecutionFailure(
                "COMMAND_TYPE_UNSUPPORTED",
                $"Command type '{command.Type}' is not supported by this agent version."));
        }

        try
        {
            return await handler.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
        }
        catch (NotSupportedException exception)
        {
            return CommandExecutionOutcome.FromFailure(new CommandExecutionFailure(
                "COMMAND_NOT_IMPLEMENTED",
                exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return CommandExecutionOutcome.FromFailure(new CommandExecutionFailure(
                "COMMAND_INVALID_OPERATION",
                exception.Message));
        }
    }
}
