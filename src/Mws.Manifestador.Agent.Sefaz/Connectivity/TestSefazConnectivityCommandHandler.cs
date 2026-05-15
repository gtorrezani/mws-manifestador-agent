using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Mws.Manifestador.Agent.Application.Certificates;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Domain.Entities;
using Mws.Manifestador.Agent.Domain.Enums;
using Mws.Manifestador.Agent.Sefaz.Endpoints;
using Mws.Manifestador.Agent.Sefaz.Models;

namespace Mws.Manifestador.Agent.Sefaz.Connectivity;

public sealed class TestSefazConnectivityCommandHandler : ICommandHandler
{
    private const string ConfigurationMode = "configuration_only";
    private const string LiveHomologationMode = "live_homologation";
    private const string SuccessMessage = "Configuração validada com sucesso.";

    private readonly ICertificateProvider certificateProvider;
    private readonly ICertificateValidator certificateValidator;
    private readonly ISefazEndpointResolver endpointResolver;

    public TestSefazConnectivityCommandHandler(
        ICertificateProvider certificateProvider,
        ICertificateValidator certificateValidator,
        ISefazEndpointResolver endpointResolver)
    {
        this.certificateProvider = certificateProvider ?? throw new ArgumentNullException(nameof(certificateProvider));
        this.certificateValidator = certificateValidator ?? throw new ArgumentNullException(nameof(certificateValidator));
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
    }

    public CommandType Type => CommandType.TestSefazConnectivity;

    public async Task<CommandExecutionOutcome> ExecuteAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        Stopwatch stopwatch = Stopwatch.StartNew();

        ConnectivityPayload payload;
        try
        {
            payload = ConnectivityPayload.From(command.Payload);
        }
        catch (InvalidOperationException exception)
        {
            return Failure("SEFAZ_CONNECTIVITY_INVALID_PAYLOAD", exception.Message, null, stopwatch);
        }

        if (string.Equals(payload.Mode, LiveHomologationMode, StringComparison.Ordinal))
        {
            return Failure(
                "SEFAZ_LIVE_TEST_NOT_CONFIGURED",
                "Live SEFAZ homologation connectivity test is not configured yet. Use configuration_only until an approved non-mutating SEFAZ probe is defined.",
                null,
                stopwatch);
        }

        if (!string.Equals(payload.Mode, ConfigurationMode, StringComparison.Ordinal))
        {
            return Failure("SEFAZ_CONNECTIVITY_INVALID_PAYLOAD", $"Unsupported connectivity test mode '{payload.Mode}'.", null, stopwatch);
        }

        return await ExecuteConfigurationOnlyAsync(payload, stopwatch, cancellationToken).ConfigureAwait(false);
    }

    private async Task<CommandExecutionOutcome> ExecuteConfigurationOnlyAsync(
        ConnectivityPayload payload,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        CertificateReference reference = CertificateReference.A3(payload.Thumbprint, payload.StoreScope);
        CertificateValidationResult certificateResult = await certificateValidator.ValidateAsync(reference, cancellationToken).ConfigureAwait(false);
        if (!certificateResult.IsValid || certificateResult.Certificate is null)
        {
            return Failure(CertificateErrorCodeToWire(certificateResult.ErrorCode), certificateResult.Message ?? "Certificate is invalid.", certificateResult.Certificate, stopwatch);
        }

        try
        {
            using X509Certificate2 certificate = await certificateProvider.GetCertificateAsync(reference, cancellationToken).ConfigureAwait(false);
            SefazEndpoint endpoint = endpointResolver.Resolve(SefazService.NFeDistribuicaoDFe, payload.Environment, payload.Uf);

            return CommandExecutionOutcome.FromResult(new CommandExecutionResult(
                true,
                new
                {
                    mode = payload.Mode,
                    environment = EnvironmentName(payload.Environment),
                    uf = payload.Uf.ToString(),
                    endpoint = endpoint.Url.ToString(),
                    certificate = ToCertificatePayload(certificateResult.Certificate),
                    sefaz_status_code = (string?)null,
                    sefaz_message = SuccessMessage,
                    duration_ms = ElapsedMilliseconds(stopwatch),
                },
                SefazMessage: SuccessMessage,
                DurationMs: ElapsedMilliseconds(stopwatch)));
        }
        catch (InvalidOperationException exception)
        {
            return Failure("SEFAZ_ENDPOINT_NOT_CONFIGURED", exception.Message, certificateResult.Certificate, stopwatch);
        }
        catch (CertificateProviderException exception)
        {
            return Failure(CertificateErrorCodeToWire(exception.ErrorCode), exception.Message, certificateResult.Certificate, stopwatch);
        }
        catch (CryptographicException exception)
        {
            return Failure("SEFAZ_CONNECTIVITY_CERTIFICATE_INVALID", exception.Message, certificateResult.Certificate, stopwatch);
        }
    }

    private static CommandExecutionOutcome Failure(
        string errorCode,
        string errorMessage,
        CertificateSummary? certificate,
        Stopwatch stopwatch)
    {
        return CommandExecutionOutcome.FromFailure(new CommandExecutionFailure(
            errorCode,
            errorMessage,
            certificate is null ? null : new { certificate = ToCertificatePayload(certificate) },
            DurationMs: ElapsedMilliseconds(stopwatch)));
    }

    private static object ToCertificatePayload(CertificateSummary certificate)
    {
        return new
        {
            thumbprint = certificate.Thumbprint,
            store_location = StoreScopeName(certificate.StoreScope),
            subject = certificate.Subject,
            not_after = FormatDate(certificate.NotAfter),
            is_valid = certificate.HasPrivateKey && certificate.NotAfter >= DateTimeOffset.UtcNow,
        };
    }

    private static string CertificateErrorCodeToWire(CertificateErrorCode errorCode)
    {
        return errorCode switch
        {
            CertificateErrorCode.CertificateNotFound => "SEFAZ_CONNECTIVITY_CERTIFICATE_NOT_FOUND",
            CertificateErrorCode.CertificateExpired => "SEFAZ_CONNECTIVITY_CERTIFICATE_INVALID",
            CertificateErrorCode.CertificateWithoutPrivateKey => "SEFAZ_CONNECTIVITY_CERTIFICATE_INVALID",
            CertificateErrorCode.CertificateInvalid => "SEFAZ_CONNECTIVITY_CERTIFICATE_INVALID",
            _ => "SEFAZ_CONNECTIVITY_CERTIFICATE_INVALID",
        };
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

    private static string EnvironmentName(SefazEnvironment environment)
    {
        return environment == SefazEnvironment.Homologation ? "homologation" : "production";
    }

    private static string FormatDate(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }

    private static int ElapsedMilliseconds(Stopwatch stopwatch)
    {
        return checked((int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds));
    }

    private sealed record ConnectivityPayload(
        string Mode,
        string Thumbprint,
        CertificateStoreScope? StoreScope,
        SefazEnvironment Environment,
        SefazUf Uf)
    {
        public static ConnectivityPayload From(JsonElement payload)
        {
            return new ConnectivityPayload(
                RequiredString(payload, "mode"),
                RequiredString(payload, "thumbprint"),
                ReadStoreScope(payload),
                ReadEnvironment(payload),
                ReadUf(payload));
        }

        private static CertificateStoreScope? ReadStoreScope(JsonElement payload)
        {
            string? value = OptionalString(payload, "store_location");

            return value switch
            {
                "CurrentUser" or "current_user" => CertificateStoreScope.CurrentUser,
                "LocalMachine" or "local_machine" => CertificateStoreScope.LocalMachine,
                _ => null,
            };
        }

        private static SefazEnvironment ReadEnvironment(JsonElement payload)
        {
            string value = RequiredString(payload, "environment");

            if (value.Equals("homologation", StringComparison.OrdinalIgnoreCase))
            {
                return SefazEnvironment.Homologation;
            }

            if (value.Equals("production", StringComparison.OrdinalIgnoreCase))
            {
                return SefazEnvironment.Production;
            }

            throw new InvalidOperationException($"Unsupported SEFAZ environment '{value}'.");
        }

        private static SefazUf ReadUf(JsonElement payload)
        {
            string value = RequiredString(payload, "uf");

            return Enum.TryParse(value, ignoreCase: true, out SefazUf uf) && uf != SefazUf.None
                ? uf
                : throw new InvalidOperationException($"Unsupported UF '{value}'.");
        }

        private static string RequiredString(JsonElement payload, string propertyName)
        {
            string? value = OptionalString(payload, propertyName);

            return string.IsNullOrWhiteSpace(value)
                ? throw new InvalidOperationException($"Command payload must include '{propertyName}'.")
                : value;
        }

        private static string? OptionalString(JsonElement payload, string propertyName)
        {
            return payload.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
    }
}
