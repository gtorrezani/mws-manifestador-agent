using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Mws.Manifestador.Agent.Application.Certificates;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Domain.Entities;
using Mws.Manifestador.Agent.Domain.Enums;

namespace Mws.Manifestador.Agent.Application.Commands;

public sealed class TestCertificateCommandHandler : ICommandHandler
{
    private const string SuccessMessage = "Certificado validado com sucesso.";

    private readonly ICertificateValidator certificateValidator;
    private readonly ICertificateProvider certificateProvider;

    public TestCertificateCommandHandler(
        ICertificateValidator certificateValidator,
        ICertificateProvider certificateProvider)
    {
        this.certificateValidator = certificateValidator ?? throw new ArgumentNullException(nameof(certificateValidator));
        this.certificateProvider = certificateProvider ?? throw new ArgumentNullException(nameof(certificateProvider));
    }

    public CommandType Type => CommandType.TestCertificate;

    public async Task<CommandExecutionOutcome> ExecuteAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        Stopwatch stopwatch = Stopwatch.StartNew();

        string? thumbprint = TryReadThumbprint(command);
        if (string.IsNullOrWhiteSpace(thumbprint))
        {
            return CommandExecutionOutcome.FromFailure(new CommandExecutionFailure(
                "CERTIFICATE_THUMBPRINT_REQUIRED",
                "The command payload must include a certificate thumbprint.",
                DurationMs: ElapsedMilliseconds(stopwatch)));
        }

        CertificateStoreScope? storeScope = TryReadStoreScope(command);
        CertificateReference reference = CertificateReference.A3(thumbprint, storeScope);
        CertificateValidationResult result = await certificateValidator
            .ValidateAsync(reference, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsValid)
        {
            return CommandExecutionOutcome.FromFailure(new CommandExecutionFailure(
                ToWireErrorCode(result.ErrorCode),
                result.Message ?? "Certificate validation failed.",
                result.Certificate,
                DurationMs: ElapsedMilliseconds(stopwatch)));
        }

        CertificateSummary certificate = result.Certificate ?? throw new InvalidOperationException("Valid certificate result must include certificate details.");
        CommandExecutionOutcome? accessFailure = await ValidatePrivateKeyAccessAsync(reference, certificate, stopwatch, cancellationToken).ConfigureAwait(false);
        if (accessFailure is not null)
        {
            return accessFailure;
        }

        return CommandExecutionOutcome.FromResult(new CommandExecutionResult(
            true,
            new
            {
                certificate = ToPayload(certificate, SuccessMessage, isValid: true),
            },
            DurationMs: ElapsedMilliseconds(stopwatch)));
    }

    private async Task<CommandExecutionOutcome?> ValidatePrivateKeyAccessAsync(
        CertificateReference reference,
        CertificateSummary certificate,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        try
        {
            using X509Certificate2 privateKeyCertificate = await certificateProvider
                .GetCertificateAsync(reference, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (CertificateProviderException exception)
        {
            return Failure(exception.ErrorCode, exception.Message, certificate, stopwatch);
        }
        catch (CryptographicException exception)
        {
            return Failure(ClassifyCryptographicException(exception), exception.Message, certificate, stopwatch);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failure(CertificateErrorCode.CertificateProviderAccessDenied, exception.Message, certificate, stopwatch);
        }

        return null;
    }

    private static CommandExecutionOutcome Failure(
        CertificateErrorCode errorCode,
        string message,
        CertificateSummary? certificate,
        Stopwatch stopwatch)
    {
        return CommandExecutionOutcome.FromFailure(new CommandExecutionFailure(
            ToWireErrorCode(errorCode),
            message,
            certificate is null ? null : ToPayload(certificate, message, isValid: false),
            DurationMs: ElapsedMilliseconds(stopwatch)));
    }

    private static object ToPayload(CertificateSummary certificate, string? validationMessage, bool isValid)
    {
        bool isExpired = certificate.NotAfter < DateTimeOffset.UtcNow;

        return new
        {
            thumbprint = certificate.Thumbprint,
            store_location = StoreScopeName(certificate.StoreScope),
            subject = certificate.Subject,
            issuer = certificate.Issuer,
            serial_number = certificate.SerialNumber,
            not_before = FormatDate(certificate.NotBefore),
            not_after = FormatDate(certificate.NotAfter),
            has_private_key = certificate.HasPrivateKey,
            cnpj = certificate.Cnpj,
            is_expired = isExpired,
            is_valid = isValid && !isExpired && certificate.HasPrivateKey,
            validation_message = validationMessage,
        };
    }

    private static CertificateStoreScope? TryReadStoreScope(AgentCommand command)
    {
        if (!command.Payload.TryGetProperty("store_location", out System.Text.Json.JsonElement element))
        {
            return null;
        }

        string? value = element.GetString();

        return value switch
        {
            "CurrentUser" or "current_user" => CertificateStoreScope.CurrentUser,
            "LocalMachine" or "local_machine" => CertificateStoreScope.LocalMachine,
            _ => null,
        };
    }

    private static string? TryReadThumbprint(AgentCommand command)
    {
        if (!command.Payload.TryGetProperty("thumbprint", out System.Text.Json.JsonElement thumbprintElement))
        {
            return null;
        }

        return thumbprintElement.GetString();
    }

    private static string StoreScopeName(CertificateStoreScope storeScope)
    {
        return storeScope switch
        {
            CertificateStoreScope.CurrentUser => "CurrentUser",
            CertificateStoreScope.LocalMachine => "LocalMachine",
            _ => "Unknown",
        };
    }

    private static string FormatDate(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }

    private static int ElapsedMilliseconds(Stopwatch stopwatch)
    {
        return checked((int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds));
    }

    private static string ToWireErrorCode(CertificateErrorCode errorCode)
    {
        return errorCode switch
        {
            CertificateErrorCode.CertificateNotFound => "CERTIFICATE_NOT_FOUND",
            CertificateErrorCode.CertificateExpired => "CERTIFICATE_EXPIRED",
            CertificateErrorCode.CertificateWithoutPrivateKey => "CERTIFICATE_WITHOUT_PRIVATE_KEY",
            CertificateErrorCode.CertificateProviderAccessDenied => "CERTIFICATE_PRIVATE_KEY_INACCESSIBLE",
            CertificateErrorCode.CertificatePinCancelled => "CERTIFICATE_PIN_CANCELLED",
            CertificateErrorCode.CertificateTokenUnavailable => "CERTIFICATE_PROVIDER_ERROR",
            CertificateErrorCode.CertificateStoreUnavailable => "CERTIFICATE_PROVIDER_ERROR",
            _ => "CERTIFICATE_PROVIDER_ERROR",
        };
    }

    private static CertificateErrorCode ClassifyCryptographicException(CryptographicException exception)
    {
        string message = exception.Message;
        if (message.Contains("cancel", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("cancelado", StringComparison.OrdinalIgnoreCase))
        {
            return CertificateErrorCode.CertificatePinCancelled;
        }

        if (message.Contains("keyset", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("smart card", StringComparison.OrdinalIgnoreCase))
        {
            return CertificateErrorCode.CertificateTokenUnavailable;
        }

        return CertificateErrorCode.CertificateProviderAccessDenied;
    }
}
