using System.Text.Json;
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
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            IReadOnlyCollection<CertificateSummary> certificates = await certificateProvider.ListAsync(cancellationToken).ConfigureAwait(false);
            bool includeExpired = GetBoolean(command, "include_expired");
            bool includeRejected = GetBoolean(command, "include_rejected");

            return CommandExecutionOutcome.FromResult(new CommandExecutionResult(
                true,
                new
                {
                    certificates = certificates
                        .Where(certificate => ShouldInclude(certificate, includeExpired, includeRejected))
                        .Select(ToListedCertificate)
                        .ToArray(),
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

        return new ListedCertificate(
            certificate.Thumbprint,
            certificate.Subject,
            certificate.Issuer,
            certificate.CommonName,
            certificate.Document,
            certificate.DocumentType,
            certificate.SerialNumber,
            FormatDate(certificate.NotBefore),
            FormatDate(certificate.NotAfter),
            FormatDate(certificate.NotBefore),
            FormatDate(certificate.NotAfter),
            certificate.HasPrivateKey,
            StoreLocation(certificate.StoreScope),
            "My",
            certificate.Cnpj,
            isExpired,
            certificate.IsCertificateAuthority,
            certificate.IsFiscalCandidate && !isExpired,
            certificate.IsIcpBrasil,
            certificate.IsUsableForClientAuth,
            certificate.Classification,
            certificate.RejectionReasons ?? [],
            certificate.Warnings ?? [],
            certificate.IsFiscalCandidate && !isExpired,
            ValidationMessage(certificate, isExpired));
    }

    private static bool ShouldInclude(CertificateSummary certificate, bool includeExpired, bool includeRejected)
    {
        bool isExpiredFiscal = string.Equals(certificate.Classification, "expired_fiscal", StringComparison.Ordinal);
        if (isExpiredFiscal)
        {
            return includeExpired;
        }

        if (includeRejected)
        {
            return true;
        }

        return certificate.IsFiscalCandidate;
    }

    private static bool GetBoolean(AgentCommand command, string name)
    {
        return command.Payload.ValueKind == JsonValueKind.Object &&
            command.Payload.TryGetProperty(name, out JsonElement value) &&
            value.ValueKind == JsonValueKind.True;
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

    private static string? ValidationMessage(CertificateSummary certificate, bool isExpired)
    {
        if (isExpired)
        {
            return "Certificate is expired.";
        }

        if (certificate.IsFiscalCandidate)
        {
            return null;
        }

        return certificate.RejectionReasons?.FirstOrDefault();
    }
}
