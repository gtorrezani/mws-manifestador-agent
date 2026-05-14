using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Mws.Manifestador.Agent.Domain.Enums;
using Mws.Manifestador.Agent.Domain.ValueObjects;
using Mws.Manifestador.Agent.Sefaz.Configuration;
using Mws.Manifestador.Agent.Sefaz.Events;
using Mws.Manifestador.Agent.Sefaz.Models;
using Mws.Manifestador.Agent.Sefaz.Xml;

namespace Mws.Manifestador.Agent.Tests.Sefaz;

public sealed class XmlSignatureTests
{
    [Fact]
    public async Task SignAsyncProducesVerifiableXmlSignature()
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest certificateRequest = new("CN=MWS Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        ManifestationXmlBuilder builder = new(Options.Create(new SefazOptions()));
        ManifestationEventRequest request = new(
            SefazUf.SP,
            SefazEnvironment.Homologation,
            new Cnpj("12345678000195"),
            new AccessKey("12345678901234567890123456789012345678901234"),
            ManifestationEventCode.OperationAcknowledgement,
            1,
            null,
            "thumb",
            "corr-1");
        string xml = builder.BuildSingle(request, "1");

        string signed = await new XmlSigner().SignAsync(xml, certificate, "Id", CancellationToken.None);

        XmlDocument document = new()
        {
            PreserveWhitespace = true,
        };
        document.LoadXml(signed);
        XmlNodeList signatures = document.GetElementsByTagName("Signature", SignedXml.XmlDsigNamespaceUrl);
        signatures.Count.Should().Be(1);

        SignedXmlWithId signedXml = new(document);
        signedXml.LoadXml((XmlElement)signatures[0]!);
        signedXml.CheckSignature(certificate, verifySignatureOnly: true).Should().BeTrue();
    }

    private sealed class SignedXmlWithId : SignedXml
    {
        public SignedXmlWithId(XmlDocument document)
            : base(document)
        {
        }

        public override XmlElement? GetIdElement(XmlDocument? document, string idValue)
        {
            if (document is null)
            {
                return null;
            }

            return base.GetIdElement(document, idValue)
                ?? document.SelectSingleNode($"//*[@Id='{idValue}']") as XmlElement;
        }
    }
}
