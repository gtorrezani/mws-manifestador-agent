using FluentAssertions;
using Mws.Manifestador.Agent.Domain.Enums;
using Mws.Manifestador.Agent.Sefaz.Endpoints;
using Mws.Manifestador.Agent.Sefaz.Models;

namespace Mws.Manifestador.Agent.Tests.Sefaz;

public sealed class SefazEndpointResolverTests
{
    [Fact]
    public void ResolveUsesNationalEndpointForDistribution()
    {
        SefazEndpoint endpoint = new SefazEndpointResolver()
            .Resolve(SefazService.NFeDistribuicaoDFe, SefazEnvironment.Production, SefazUf.SP);

        endpoint.Url.ToString().Should().Contain("NFeDistribuicaoDFe");
        endpoint.Uf.Should().Be(SefazUf.AN);
    }
}
