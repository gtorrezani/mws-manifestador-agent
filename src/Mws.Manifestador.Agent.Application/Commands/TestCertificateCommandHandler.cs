using Mws.Manifestador.Agent.Application.Certificates;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Domain.Entities;
using Mws.Manifestador.Agent.Domain.Enums;

namespace Mws.Manifestador.Agent.Application.Commands;

public sealed class TestCertificateCommandHandler : ICommandHandler
{
    private readonly ICertificateValidator certificateValidator;

    public TestCertificateCommandHandler(ICertificateValidator certificateValidator)
    {
        this.certificateValidator = certificateValidator ?? throw new ArgumentNullException(nameof(certificateValidator));
    }

    public CommandType Type => CommandType.TestCertificate;

    public async Task<CommandExecutionOutcome> ExecuteAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!command.Payload.TryGetProperty("thumbprint", out System.Text.Json.JsonElement thumbprintElement))
        {
            return CommandExecutionOutcome.FromFailure(new CommandExecutionFailure(
                "CERTIFICATE_THUMBPRINT_REQUIRED",
                "The command payload must include a certificate thumbprint."));
        }

        string? thumbprint = thumbprintElement.GetString();
        if (string.IsNullOrWhiteSpace(thumbprint))
        {
            return CommandExecutionOutcome.FromFailure(new CommandExecutionFailure(
                "CERTIFICATE_THUMBPRINT_REQUIRED",
                "The command payload must include a certificate thumbprint."));
        }

        CertificateValidationResult result = await certificateValidator
            .ValidateAsync(CertificateReference.A3(thumbprint), cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsValid)
        {
            return CommandExecutionOutcome.FromFailure(new CommandExecutionFailure(
                result.ErrorCode.ToString(),
                result.Message ?? "Certificate validation failed.",
                result.Certificate));
        }

        return CommandExecutionOutcome.FromResult(new CommandExecutionResult(true, result.Certificate));
    }
}
