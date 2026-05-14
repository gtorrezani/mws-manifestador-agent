using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Domain.Common;
using Mws.Manifestador.Agent.Domain.Entities;
using Mws.Manifestador.Agent.Domain.Enums;

namespace Mws.Manifestador.Agent.Application.Commands;

public sealed class SefazCommandHandler : ICommandHandler
{
    private readonly ISefazClient sefazClient;

    public SefazCommandHandler(CommandType type, ISefazClient sefazClient)
    {
        Type = type;
        this.sefazClient = sefazClient;
    }

    public CommandType Type { get; }

    public async Task<CommandExecutionOutcome> ExecuteAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<CommandExecutionResult> result = Type switch
        {
            CommandType.SyncFiscalDocuments => await sefazClient.SyncFiscalDocumentsAsync(command, cancellationToken).ConfigureAwait(false),
            CommandType.ManifestAcknowledgement or CommandType.ManifestConfirmation or CommandType.ManifestUnknown or CommandType.ManifestNotPerformed
                => await sefazClient.SendManifestationAsync(command, cancellationToken).ConfigureAwait(false),
            CommandType.DownloadXmlByAccessKey or CommandType.DownloadXmlByPeriod or CommandType.ExportXmlZip
                => await sefazClient.DownloadXmlAsync(command, cancellationToken).ConfigureAwait(false),
            CommandType.TestSefazConnectivity => await sefazClient.TestConnectivityAsync(command, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException($"SEFAZ command type '{Type}' is not supported."),
        };

        if (!result.IsSuccess || result.Value is null)
        {
            return CommandExecutionOutcome.FromFailure(new CommandExecutionFailure(
                result.ErrorCode ?? "SEFAZ_ERROR",
                result.ErrorMessage ?? "SEFAZ operation failed without details."));
        }

        return CommandExecutionOutcome.FromResult(result.Value);
    }
}
