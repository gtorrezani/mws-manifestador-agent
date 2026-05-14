using Microsoft.Extensions.Options;
using Mws.Manifestador.Agent.Application.Configuration;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Domain.Entities;
using Mws.Manifestador.Agent.Domain.Enums;

namespace Mws.Manifestador.Agent.Application.Services;

public sealed class PollingService
{
    private static readonly CommandType[] Capabilities =
    [
        CommandType.SyncFiscalDocuments,
        CommandType.ManifestAcknowledgement,
        CommandType.ManifestConfirmation,
        CommandType.ManifestUnknown,
        CommandType.ManifestNotPerformed,
        CommandType.DownloadXmlByAccessKey,
        CommandType.DownloadXmlByPeriod,
        CommandType.ExportXmlZip,
        CommandType.TestCertificate,
        CommandType.ListCertificates,
        CommandType.TestSefazConnectivity,
    ];

    private readonly IAgentApiClient apiClient;
    private readonly CommandExecutor executor;
    private readonly AgentPollingOptions options;

    public PollingService(
        IAgentApiClient apiClient,
        CommandExecutor executor,
        IOptions<AgentPollingOptions> options)
    {
        this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<int> PollAndExecuteOnceAsync(AgentCredentials credentials, CancellationToken cancellationToken)
    {
        PollCommandsRequest request = new(options.MaxCommandsPerPoll, Capabilities);
        IReadOnlyCollection<AgentCommand> commands = await apiClient.PollCommandsAsync(credentials, request, cancellationToken).ConfigureAwait(false);

        foreach (AgentCommand command in commands)
        {
            await apiClient.StartCommandAsync(credentials, command.Uuid, cancellationToken).ConfigureAwait(false);
            CommandExecutionOutcome outcome = await executor.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);

            if (outcome.Succeeded && outcome.Result is not null)
            {
                await apiClient.CompleteCommandAsync(credentials, command.Uuid, outcome.Result, cancellationToken).ConfigureAwait(false);
                continue;
            }

            CommandExecutionFailure failure = outcome.Failure ?? new CommandExecutionFailure(
                "COMMAND_FAILED_WITHOUT_DETAILS",
                "Command failed without details.");
            await apiClient.FailCommandAsync(credentials, command.Uuid, failure, cancellationToken).ConfigureAwait(false);
        }

        return commands.Count;
    }
}
