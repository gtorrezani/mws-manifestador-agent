using FluentAssertions;
using Microsoft.Extensions.Options;
using Mws.Manifestador.Agent.Domain.Enums;
using Mws.Manifestador.Agent.Domain.ValueObjects;
using Mws.Manifestador.Agent.Sefaz.Configuration;
using Mws.Manifestador.Agent.Sefaz.Distribution;
using Mws.Manifestador.Agent.Sefaz.Events;
using Mws.Manifestador.Agent.Sefaz.Models;

namespace Mws.Manifestador.Agent.Tests.Sefaz;

public sealed class SefazXmlBuilderTests
{
    [Fact]
    public void DistributionBuilderBuildsDistNsuRequest()
    {
        DistributionQuery query = new(
            SefazUf.SP,
            SefazEnvironment.Homologation,
            new Cnpj("12345678000195"),
            "12",
            null,
            null,
            "thumb",
            null,
            "corr-1");

        string xml = new DistributionXmlBuilder().Build(query);

        xml.Should().Contain("<distDFeInt");
        xml.Should().Contain("<ultNSU>000000000000012</ultNSU>");
        xml.Should().Contain("<tpAmb>2</tpAmb>");
    }

    [Fact]
    public void ManifestationBuilderRequiresJustificationForOperationNotPerformed()
    {
        ManifestationXmlBuilder builder = new(Options.Create(new SefazOptions()));
        ManifestationEventRequest request = new(
            SefazUf.SP,
            SefazEnvironment.Production,
            new Cnpj("12345678000195"),
            new AccessKey("12345678901234567890123456789012345678901234"),
            ManifestationEventCode.OperationNotPerformed,
            1,
            null,
            "thumb",
            "corr-1");

        Action act = () => builder.BuildSingle(request, "1");

        act.Should().Throw<InvalidOperationException>().WithMessage("*justification*");
    }
}
