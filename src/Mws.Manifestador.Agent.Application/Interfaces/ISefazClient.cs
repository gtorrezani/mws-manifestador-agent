using Mws.Manifestador.Agent.Domain.Common;
using Mws.Manifestador.Agent.Domain.Entities;

namespace Mws.Manifestador.Agent.Application.Interfaces;

public interface ISefazClient
{
    Task<Result<CommandExecutionResult>> SyncFiscalDocumentsAsync(AgentCommand command, CancellationToken cancellationToken);

    Task<Result<CommandExecutionResult>> SendManifestationAsync(AgentCommand command, CancellationToken cancellationToken);

    Task<Result<CommandExecutionResult>> DownloadXmlAsync(AgentCommand command, CancellationToken cancellationToken);

    Task<Result<CommandExecutionResult>> TestConnectivityAsync(AgentCommand command, CancellationToken cancellationToken);
}
