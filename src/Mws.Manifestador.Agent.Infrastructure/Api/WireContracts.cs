using System.Text.Json;
using Mws.Manifestador.Agent.Application.DTOs;

namespace Mws.Manifestador.Agent.Infrastructure.Api;

public sealed record ActivationWireRequest(
    string ActivationCode,
    string InstallationId,
    string MachineName,
    string Version,
    IReadOnlyCollection<CertificateInfo> CertificateInventory);

public sealed record ActivationWireResponse(
    Guid AgentId,
    string Secret,
    ActivationAuthWireResponse Auth,
    int PollingIntervalSeconds);

public sealed record ActivationAuthWireResponse(int TimestampToleranceSeconds);

public sealed record PollCommandsWireRequest(int Limit, IReadOnlyCollection<string> Capabilities);

public sealed record PollCommandsWireResponse(IReadOnlyCollection<CommandWireResponse> Commands);

public sealed record CommandWireResponse(
    Guid Uuid,
    string Type,
    int Priority,
    JsonElement Payload,
    string? IdempotencyKey,
    DateTimeOffset? LockExpiresAt,
    int AttemptsCount,
    int MaxAttempts);
