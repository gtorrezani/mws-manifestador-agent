namespace Mws.Manifestador.Agent.Sefaz.Models;

public sealed record SefazResponseMetadata(
    string? StatusCode,
    string? Reason,
    string? ProtocolNumber,
    string RawXml);
