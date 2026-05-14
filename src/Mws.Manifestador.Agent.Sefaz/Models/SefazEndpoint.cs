using Mws.Manifestador.Agent.Domain.Enums;

namespace Mws.Manifestador.Agent.Sefaz.Models;

public sealed record SefazEndpoint(
    SefazService Service,
    SefazEnvironment Environment,
    SefazUf Uf,
    Uri Url,
    string SoapAction,
    string OperationName,
    string OperationNamespace);
