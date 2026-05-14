namespace Mws.Manifestador.Agent.Sefaz.Models;

public sealed record FiscalDocumentFull(
    string AccessKey,
    string IssuerCnpj,
    string IssuerName,
    string RecipientCnpj,
    string Number,
    string Series,
    DateTimeOffset IssuedAt,
    decimal TotalAmount);
