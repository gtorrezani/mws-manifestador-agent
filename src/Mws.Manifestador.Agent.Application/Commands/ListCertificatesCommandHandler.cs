using Mws.Manifestador.Agent.Application.Certificates;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Domain.Entities;
using Mws.Manifestador.Agent.Domain.Enums;

namespace Mws.Manifestador.Agent.Application.Commands;

public sealed class ListCertificatesCommandHandler : ICommandHandler
{
    private readonly ICertificateProvider certificateProvider;

    public ListCertificatesCommandHandler(ICertificateProvider certificateProvider)
    {
        this.certificateProvider = certificateProvider ?? throw new ArgumentNullException(nameof(certificateProvider));
    }

    public CommandType Type => CommandType.ListCertificates;

    public async Task<CommandExecutionOutcome> ExecuteAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<CertificateSummary> certificates = await certificateProvider.ListAsync(cancellationToken).ConfigureAwait(false);

        return CommandExecutionOutcome.FromResult(new CommandExecutionResult(
            true,
            new
            {
                certificates,
            }));
    }
}
