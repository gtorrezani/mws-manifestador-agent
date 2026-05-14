using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Mws.Manifestador.Agent.Application.Commands;
using Mws.Manifestador.Agent.Application.Configuration;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Domain.Entities;

namespace Mws.Manifestador.Agent.Infrastructure.Api;

public sealed class LaravelAgentApiClient : IAgentApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly HttpClient httpClient;

    public LaravelAgentApiClient(
        HttpClient httpClient,
        IOptions<AgentApiOptions> options)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.httpClient.BaseAddress = options?.Value.BaseUrl ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<ActivationResponse> ActivateAsync(ActivationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ActivationWireRequest wireRequest = new(
            request.ActivationCode,
            request.InstallationId,
            request.MachineName,
            request.Version,
            request.CertificateInventory);

        HttpResponseMessage response = await PostJsonAsync("/api/agent/v1/activate", wireRequest, null, cancellationToken).ConfigureAwait(false);
        ActivationWireResponse? payload = await response.Content.ReadFromJsonAsync<ActivationWireResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);

        if (payload is null)
        {
            throw new InvalidOperationException("Activation response is empty.");
        }

        return new ActivationResponse(payload.AgentId, payload.Secret, payload.PollingIntervalSeconds, payload.Auth.TimestampToleranceSeconds);
    }

    public async Task SendHeartbeatAsync(AgentCredentials credentials, HeartbeatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(request);

        await PostJsonAsync("/api/agent/v1/heartbeat", request, credentials, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<AgentCommand>> PollCommandsAsync(AgentCredentials credentials, PollCommandsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(request);

        PollCommandsWireRequest wireRequest = new(
            request.Limit,
            request.Capabilities.Select(CommandTypeNames.ToWireName).ToArray());

        HttpResponseMessage response = await PostJsonAsync("/api/agent/v1/commands/poll", wireRequest, credentials, cancellationToken).ConfigureAwait(false);
        PollCommandsWireResponse? payload = await response.Content.ReadFromJsonAsync<PollCommandsWireResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);

        return payload?.Commands.Select(static command => new AgentCommand(
            command.Uuid,
            CommandTypeNames.FromWireName(command.Type),
            command.Priority,
            command.Payload,
            command.IdempotencyKey,
            command.LockExpiresAt,
            command.AttemptsCount,
            command.MaxAttempts)).ToArray() ?? [];
    }

    public async Task StartCommandAsync(AgentCredentials credentials, Guid commandId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        await PostJsonAsync($"/api/agent/v1/commands/{commandId}/start", new { }, credentials, cancellationToken).ConfigureAwait(false);
    }

    public async Task CompleteCommandAsync(AgentCredentials credentials, Guid commandId, CommandExecutionResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(result);

        object payload = new
        {
            result = result.Result,
            protocol_number = result.ProtocolNumber,
            sefaz_status_code = result.SefazStatusCode,
            sefaz_message = result.SefazMessage,
            request_xml = result.RequestXml,
            response_xml = result.ResponseXml,
            duration_ms = result.DurationMs,
        };

        await PostJsonAsync($"/api/agent/v1/commands/{commandId}/complete", payload, credentials, cancellationToken).ConfigureAwait(false);
    }

    public async Task FailCommandAsync(AgentCredentials credentials, Guid commandId, CommandExecutionFailure failure, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(failure);

        object payload = new
        {
            error_code = failure.ErrorCode,
            error_message = failure.ErrorMessage,
            error_details = failure.ErrorDetails,
            sefaz_status_code = failure.SefazStatusCode,
            sefaz_message = failure.SefazMessage,
            duration_ms = failure.DurationMs,
        };

        await PostJsonAsync($"/api/agent/v1/commands/{commandId}/fail", payload, credentials, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> PostJsonAsync<TPayload>(
        string path,
        TPayload payload,
        AgentCredentials? credentials,
        CancellationToken cancellationToken)
    {
        string body = JsonSerializer.Serialize(payload, JsonOptions);
        using HttpRequestMessage request = new(HttpMethod.Post, path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        if (credentials is not null)
        {
            HmacSignedRequest signed = HmacSignatureService.Sign(credentials.Secret, HttpMethod.Post, path, body);
            request.Headers.Add("X-MWS-Agent-Id", credentials.AgentId.ToString("D"));
            request.Headers.Add("X-MWS-Timestamp", signed.Timestamp);
            request.Headers.Add("X-MWS-Nonce", signed.Nonce);
            request.Headers.Add("X-MWS-Body-SHA256", signed.BodyHash);
            request.Headers.Add("X-MWS-Signature", signed.Signature);
        }

        HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowAgentApiExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    private static async Task ThrowAgentApiExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        string sanitizedBody = SanitizeResponseBody(body);
        string? correlationId = GetCorrelationId(response);
        string message = $"Agent API request failed with status {(int)response.StatusCode} ({response.ReasonPhrase}).";

        throw new AgentApiException(response.StatusCode, sanitizedBody, correlationId, message);
    }

    private static string? GetCorrelationId(HttpResponseMessage response)
    {
        foreach (string headerName in new[] { "X-MWS-Correlation-Id", "X-Correlation-Id", "X-Request-Id" })
        {
            if (response.Headers.TryGetValues(headerName, out IEnumerable<string>? values))
            {
                return values.FirstOrDefault();
            }
        }

        return null;
    }

    private static string SanitizeResponseBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        try
        {
            JsonNode? node = JsonNode.Parse(body);
            RedactSensitiveValues(node);

            return Truncate(node?.ToJsonString(JsonOptions) ?? string.Empty);
        }
        catch (JsonException)
        {
            return Truncate(body);
        }
    }

    private static void RedactSensitiveValues(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (KeyValuePair<string, JsonNode?> item in jsonObject.ToArray())
            {
                if (IsSensitiveKey(item.Key))
                {
                    jsonObject[item.Key] = "[redacted]";
                    continue;
                }

                RedactSensitiveValues(item.Value);
            }
        }

        if (node is JsonArray jsonArray)
        {
            foreach (JsonNode? item in jsonArray)
            {
                RedactSensitiveValues(item);
            }
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        return key.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || key.Contains("password", StringComparison.OrdinalIgnoreCase)
            || key.Contains("token", StringComparison.OrdinalIgnoreCase)
            || key.Contains("pin", StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string value)
    {
        const int MaxLength = 4096;

        return value.Length <= MaxLength ? value : value[..MaxLength];
    }
}
