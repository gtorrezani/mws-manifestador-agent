using System.Text.Json;
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

    [Fact]
    public void SignMatchesSharedPhpContractFixture()
    {
        HmacContractFixture fixture = ReadFixture();

        HmacSignedRequest signed = HmacSignatureService.Sign(
            fixture.Secret,
            new HttpMethod(fixture.Method),
            fixture.Path,
            fixture.Body,
            fixture.Timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture),
            fixture.Nonce);

        signed.BodyHash.Should().Be(fixture.BodySha256);
        signed.Signature.Should().Be(fixture.Signature);
    }

    private static HmacContractFixture ReadFixture()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "agent-hmac-contract.json");
        string content = File.ReadAllText(path);

        using JsonDocument document = JsonDocument.Parse(content);
        JsonElement root = document.RootElement;

        return new HmacContractFixture(
            ReadString(root, "secret"),
            ReadString(root, "method"),
            ReadString(root, "path"),
            root.GetProperty("timestamp").GetInt64(),
            ReadString(root, "nonce"),
            ReadString(root, "body"),
            ReadString(root, "body_sha256"),
            ReadString(root, "signature"));
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.GetProperty(propertyName).GetString()
            ?? throw new InvalidOperationException($"HMAC contract fixture property '{propertyName}' is missing.");
    }

    private sealed record HmacContractFixture(
        string Secret,
        string Method,
        string Path,
        long Timestamp,
        string Nonce,
        string Body,
        string BodySha256,
        string Signature);
}
