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
        try
        {
            IReadOnlyCollection<CertificateSummary> certificates = await certificateProvider.ListAsync(cancellationToken).ConfigureAwait(false);

            return CommandExecutionOutcome.FromResult(new CommandExecutionResult(
                true,
                new
                {
                    certificates = certificates.Select(ToListedCertificate).ToArray(),
                }));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return CommandExecutionOutcome.FromFailure(new CommandExecutionFailure(
                "CERTIFICATE_STORE_LIST_FAILED",
                "Unable to list certificates from the Windows Certificate Store.",
                new { exception_type = exception.GetType().Name }));
        }
    }

    private static ListedCertificate ToListedCertificate(CertificateSummary certificate)
    {
        bool isExpired = certificate.NotAfter < DateTimeOffset.UtcNow;
        bool isValid = certificate.HasPrivateKey && !isExpired;

        return new ListedCertificate(
            certificate.Subject,
            certificate.Issuer,
            certificate.Thumbprint,
            certificate.SerialNumber,
            FormatDate(certificate.NotBefore),
            FormatDate(certificate.NotAfter),
            certificate.HasPrivateKey,
            StoreLocation(certificate.StoreScope),
            certificate.Cnpj,
            isExpired,
            isValid,
            ValidationMessage(certificate.HasPrivateKey, isExpired));
    }

    private static string FormatDate(DateTimeOffset value)
    {
        return value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string StoreLocation(CertificateStoreScope scope)
    {
        return scope switch
        {
            CertificateStoreScope.CurrentUser => "CurrentUser",
            CertificateStoreScope.LocalMachine => "LocalMachine",
            _ => "Unknown",
        };
    }

    private static string? ValidationMessage(bool hasPrivateKey, bool isExpired)
    {
        if (isExpired)
        {
            return "Certificate is expired.";
        }

        return hasPrivateKey ? null : "Certificate does not have a private key.";
    }
}
