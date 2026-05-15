using Mws.Manifestador.Agent.Domain.Enums;
using Mws.Manifestador.Agent.Sefaz.Soap;

namespace Mws.Manifestador.Agent.Sefaz.Models;

public sealed record SefazEndpoint(
    SefazService Service,
    SefazEnvironment Environment,
    SefazUf Uf,
    Uri Url,
    string SoapAction,
    string OperationName,
    string OperationNamespace,
    SoapVersion SoapVersion = SoapVersion.Soap12);
