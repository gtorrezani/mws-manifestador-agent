using FluentAssertions;
using Mws.Manifestador.Agent.Infrastructure.Api;

namespace Mws.Manifestador.Agent.Tests.Infrastructure;

public sealed class HmacSignatureServiceTests
{
    [Fact]
    public void SignIncludesBodyHashAndSignature()
    {
        HmacSignedRequest signed = HmacSignatureService.Sign(
            "secret",
            HttpMethod.Post,
            "/api/agent/v1/heartbeat",
            "{\"status\":\"online\"}");

        signed.BodyHash.Should().HaveLength(64);
        signed.Signature.Should().HaveLength(64);
        signed.Nonce.Should().NotBeNullOrWhiteSpace();
        signed.Timestamp.Should().MatchRegex("^\\d+$");
    }
}
