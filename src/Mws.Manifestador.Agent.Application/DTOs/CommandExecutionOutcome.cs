using Mws.Manifestador.Agent.Domain.Entities;

namespace Mws.Manifestador.Agent.Application.DTOs;

public sealed record CommandExecutionOutcome(
    CommandExecutionResult? Result,
    CommandExecutionFailure? Failure)
{
    public bool Succeeded => Result is not null && Failure is null;

    public static CommandExecutionOutcome FromResult(CommandExecutionResult result) => new(result, null);

    public static CommandExecutionOutcome FromFailure(CommandExecutionFailure failure) => new(null, failure);
}
