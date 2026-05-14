namespace Mws.Manifestador.Agent.Sefaz.Models;

public sealed record DistributionResponse(
    SefazResponseMetadata Metadata,
    string LastNsu,
    string MaxNsu,
    IReadOnlyCollection<DistributedDocument> Documents);
