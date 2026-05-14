namespace Mws.Manifestador.Agent.Domain.Entities;

public sealed record CommandExecutionResult(
    bool Success,
    object? Result,
    string? ProtocolNumber = null,
    string? SefazStatusCode = null,
    string? SefazMessage = null,
    XmlArtifact? RequestXml = null,
    XmlArtifact? ResponseXml = null,
    int? DurationMs = null);
