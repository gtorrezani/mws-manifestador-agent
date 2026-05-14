using FluentAssertions;
using Mws.Manifestador.Agent.Application.Certificates;

namespace Mws.Manifestador.Agent.Tests.Certificates;

public sealed class CertificateReferenceTests
{
    [Fact]
    public void A3NormalizesThumbprintAndDoesNotContainSecret()
    {
        CertificateReference reference = CertificateReference.A3(" ab cd ", CertificateStoreScope.CurrentUser, "12345678000195");

        reference.Kind.Should().Be(CertificateKind.A3);
        reference.Thumbprint.Should().Be("ABCD");
        reference.CompanyDocument.Should().Be("12345678000195");
    }

    [Fact]
    public void CertificateSecretIsSeparateFromReference()
    {
        CertificateReference reference = CertificateReference.A1(" ab cd ");
        CertificateSecret secret = new(CertificateKind.A1, "protected-payload", "windows-dpapi-local-machine", DateTimeOffset.UtcNow);

        reference.Thumbprint.Should().Be("ABCD");
        secret.ProtectedPayload.Should().NotBe(reference.Thumbprint);
    }
}
