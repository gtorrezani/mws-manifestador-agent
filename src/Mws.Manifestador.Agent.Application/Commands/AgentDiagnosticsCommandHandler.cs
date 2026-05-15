using Mws.Manifestador.Agent.Application.Diagnostics;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Domain.Entities;
using Mws.Manifestador.Agent.Domain.Enums;

namespace Mws.Manifestador.Agent.Application.Commands;

public sealed class AgentDiagnosticsCommandHandler : ICommandHandler
{
    private readonly AgentDiagnosticsCollector collector;

    public AgentDiagnosticsCommandHandler(AgentDiagnosticsCollector collector)
    {
        this.collector = collector ?? throw new ArgumentNullException(nameof(collector));
    }

    public CommandType Type => CommandType.AgentDiagnosticsRequested;

    public async Task<CommandExecutionOutcome> ExecuteAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        AgentDiagnosticsSnapshot diagnostics = await collector.CollectAsync(cancellationToken).ConfigureAwait(false);

        return CommandExecutionOutcome.FromResult(new CommandExecutionResult(
            true,
            new
            {
                agent = new
                {
                    diagnostics.Version,
                    diagnostics.MachineName,
                    diagnostics.InstallationId,
                    diagnostics.ServiceUptimeSeconds,
                    diagnostics.ExecutionMode,
                },
                api = new
                {
                    BaseUrl = diagnostics.ApiBaseUrl,
                },
                certificates = new
                {
                    InventoryCount = diagnostics.CertificateInventoryCount,
                    StoreAccessStatus = diagnostics.StoreAccessStatus,
                    diagnostics.StoreAccessErrorCode,
                },
                os = new
                {
                    Version = diagnostics.OsVersion,
                    diagnostics.CurrentProcessUser,
                },
            }));
    }
}
