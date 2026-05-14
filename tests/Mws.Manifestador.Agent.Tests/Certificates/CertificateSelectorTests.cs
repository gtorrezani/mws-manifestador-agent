using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Mws.Manifestador.Agent.Application.Certificates;

namespace Mws.Manifestador.Agent.Tests.Certificates;

public sealed class CertificateSelectorTests
{
    [Fact]
    public async Task SelectForCompanyAsyncUsesConfiguredThumbprint()
    {
        Dictionary<string, string?> values = new(StringComparer.Ordinal)
        {
            ["Certificates:Companies:12345678000195:Thumbprint"] = "ab cd",
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        InMemoryCompanyCertificateSelector selector = new(configuration);

        CertificateReference reference = await selector.SelectForCompanyAsync("12.345.678/0001-95", CancellationToken.None);

        reference.Kind.Should().Be(CertificateKind.A3);
        reference.Thumbprint.Should().Be("ABCD");
        reference.CompanyDocument.Should().Be("12345678000195");
    }
}
