using Mws.Manifestador.Agent.Domain.Enums;
using Mws.Manifestador.Agent.Sefaz.Models;

namespace Mws.Manifestador.Agent.Sefaz.Endpoints;

public interface ISefazEndpointResolver
{
    SefazEndpoint Resolve(SefazService service, SefazEnvironment environment, SefazUf uf);
}
