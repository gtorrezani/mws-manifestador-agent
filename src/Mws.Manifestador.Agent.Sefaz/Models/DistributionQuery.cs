using Mws.Manifestador.Agent.Domain.Enums;
using Mws.Manifestador.Agent.Domain.ValueObjects;

namespace Mws.Manifestador.Agent.Sefaz.Models;

public sealed record DistributionQuery(
    SefazUf Uf,
    SefazEnvironment Environment,
    Cnpj Cnpj,
    string? LastNsu,
    string? Nsu,
    AccessKey? AccessKey,
    string CertificateThumbprint,
    string CorrelationId);
