using System.Text.Json;
using Mws.Manifestador.Agent.Domain.Enums;

namespace Mws.Manifestador.Agent.Domain.Entities;

public sealed record AgentCommand(
    Guid Uuid,
    CommandType Type,
    int Priority,
    JsonElement Payload,
    string? IdempotencyKey,
    DateTimeOffset? LockExpiresAt,
    int AttemptsCount,
    int MaxAttempts);
