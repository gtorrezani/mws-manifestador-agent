namespace Mws.Manifestador.Agent.Sefaz.Models;

public sealed record EventReceptionResponse(
    SefazResponseMetadata Metadata,
    string? EventStatusCode,
    string? EventReason,
    string? EventProtocolNumber);
