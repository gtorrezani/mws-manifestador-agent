namespace Mws.Manifestador.Agent.Domain.Entities;

public sealed record CommandExecutionFailure(
    string ErrorCode,
    string ErrorMessage,
    object? ErrorDetails = null,
    string? SefazStatusCode = null,
    string? SefazMessage = null,
    int? DurationMs = null);
