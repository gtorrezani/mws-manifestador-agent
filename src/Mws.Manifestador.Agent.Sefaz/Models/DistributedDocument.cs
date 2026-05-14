namespace Mws.Manifestador.Agent.Sefaz.Models;

public sealed record DistributedDocument(
    string Schema,
    string Nsu,
    string? AccessKey,
    string Xml,
    FiscalDocumentSummary? Summary,
    FiscalDocumentFull? FullDocument);
