using Mws.Manifestador.Agent.Domain.Enums;
using Mws.Manifestador.Agent.Domain.ValueObjects;

namespace Mws.Manifestador.Agent.Sefaz.Models;

public sealed record ManifestationEventRequest(
    SefazUf Uf,
    SefazEnvironment Environment,
    Cnpj Cnpj,
    AccessKey AccessKey,
    ManifestationEventCode EventCode,
    int Sequence,
    string? Justification,
    string CertificateThumbprint,
    string CorrelationId);
