using System.Text.Json;
using FluentAssertions;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Application.Services;
using Mws.Manifestador.Agent.Domain.Entities;
using Mws.Manifestador.Agent.Domain.Enums;

namespace Mws.Manifestador.Agent.Tests.Application;

public sealed class CommandExecutorTests
{
    [Fact]
    public async Task ExecuteAsyncReturnsExplicitFailureForUnsupportedCommand()
    {
        CommandExecutor executor = new([]);
        using JsonDocument payload = JsonDocument.Parse("{}");
        AgentCommand command = new(
            Guid.NewGuid(),
            CommandType.SyncFiscalDocuments,
            100,
            payload.RootElement.Clone(),
            null,
            null,
            0,
            3);

        CommandExecutionOutcome outcome = await executor.ExecuteAsync(command, CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Failure?.ErrorCode.Should().Be("COMMAND_TYPE_UNSUPPORTED");
    }

    [Fact]
    public async Task ExecuteAsyncRoutesToRegisteredHandler()
    {
        ICommandHandler handler = new SuccessfulHandler();
        CommandExecutor executor = new([handler]);
        using JsonDocument payload = JsonDocument.Parse("{}");
        AgentCommand command = new(
            Guid.NewGuid(),
            CommandType.ListCertificates,
            100,
            payload.RootElement.Clone(),
            null,
            null,
            0,
            3);

        CommandExecutionOutcome outcome = await executor.ExecuteAsync(command, CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();
    }

    private sealed class SuccessfulHandler : ICommandHandler
    {
        public CommandType Type => CommandType.ListCertificates;

        public Task<CommandExecutionOutcome> ExecuteAsync(AgentCommand command, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CommandExecutionOutcome.FromResult(new CommandExecutionResult(true, new { ok = true })));
        }
    }
}
