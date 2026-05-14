namespace Mws.Manifestador.Agent.Sefaz.Models;

public sealed record FiscalDocumentSummary(
    string AccessKey,
    string IssuerCnpj,
    string IssuerName,
    string RecipientCnpj,
    DateTimeOffset IssuedAt,
    decimal TotalAmount,
    string Situation);
