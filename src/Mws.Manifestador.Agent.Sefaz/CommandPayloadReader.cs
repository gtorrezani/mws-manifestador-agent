using System.Text.Json;
using Mws.Manifestador.Agent.Application.Certificates;
using Mws.Manifestador.Agent.Domain.Entities;
using Mws.Manifestador.Agent.Domain.Enums;
using Mws.Manifestador.Agent.Domain.ValueObjects;
using Mws.Manifestador.Agent.Sefaz.Models;

namespace Mws.Manifestador.Agent.Sefaz;

public sealed class CommandPayloadReader
{
    public DistributionQuery ReadDistributionQuery(AgentCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        JsonElement payload = command.Payload;
        return new DistributionQuery(
            ReadUf(payload),
            ReadEnvironment(payload),
            new Cnpj(GetRequiredString(payload, "cnpj")),
            GetOptionalString(payload, "last_nsu"),
            GetOptionalString(payload, "nsu"),
            GetOptionalString(payload, "access_key") is { } accessKey ? new AccessKey(accessKey) : null,
            GetRequiredString(payload, "certificate_thumbprint"),
            ReadStoreScope(payload),
            ReadCorrelationId(payload, command));
    }

    public ManifestationEventRequest ReadManifestationRequest(AgentCommand command, ManifestationEventCode eventCode)
    {
        ArgumentNullException.ThrowIfNull(command);

        JsonElement payload = command.Payload;
        return new ManifestationEventRequest(
            ReadUf(payload),
            ReadEnvironment(payload),
            new Cnpj(GetRequiredString(payload, "cnpj")),
            new AccessKey(GetRequiredString(payload, "access_key")),
            eventCode,
            GetOptionalInt(payload, "sequence") ?? 1,
            GetOptionalString(payload, "justification"),
            GetRequiredString(payload, "certificate_thumbprint"),
            ReadCorrelationId(payload, command));
    }

    private static SefazUf ReadUf(JsonElement payload)
    {
        string value = GetRequiredString(payload, "uf");
        if (!Enum.TryParse(value, ignoreCase: true, out SefazUf uf))
        {
            throw new InvalidOperationException($"Unsupported UF '{value}'.");
        }

        return uf;
    }

    private static SefazEnvironment ReadEnvironment(JsonElement payload)
    {
        string? value = GetOptionalString(payload, "environment");
        return string.Equals(value, "homologation", StringComparison.OrdinalIgnoreCase)
            ? SefazEnvironment.Homologation
            : SefazEnvironment.Production;
    }

    private static string ReadCorrelationId(JsonElement payload, AgentCommand command)
    {
        return GetOptionalString(payload, "correlation_id") ?? command.Uuid.ToString("D");
    }

    private static CertificateStoreScope? ReadStoreScope(JsonElement payload)
    {
        string? value = GetOptionalString(payload, "store_location");

        return value switch
        {
            "CurrentUser" or "current_user" => CertificateStoreScope.CurrentUser,
            "LocalMachine" or "local_machine" => CertificateStoreScope.LocalMachine,
            _ => null,
        };
    }

    private static string GetRequiredString(JsonElement payload, string propertyName)
    {
        string? value = GetOptionalString(payload, propertyName);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Command payload must include '{propertyName}'.")
            : value;
    }

    private static string? GetOptionalString(JsonElement payload, string propertyName)
    {
        return payload.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? GetOptionalInt(JsonElement payload, string propertyName)
    {
        return payload.TryGetProperty(propertyName, out JsonElement value) && value.TryGetInt32(out int parsed)
            ? parsed
            : null;
    }
}
