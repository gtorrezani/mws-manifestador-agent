using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using Mws.Manifestador.Agent.Application.Interfaces;

namespace Mws.Manifestador.Agent.Sefaz.Xml;

public sealed class XmlSigner : IXmlSigner
{
    public Task<string> SignAsync(
        string xmlContent,
        X509Certificate2 certificate,
        string referenceIdAttribute,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using RSA? privateKey = certificate.GetRSAPrivateKey();
        if (privateKey is null)
        {
            throw new InvalidOperationException("Certificate private key is not accessible.");
        }

        XmlDocument document = new()
        {
            PreserveWhitespace = true,
        };
        document.LoadXml(xmlContent);

        XmlElement elementToSign = FindElementToSign(document, referenceIdAttribute);
        string? referenceId = elementToSign.GetAttribute(referenceIdAttribute);
        if (string.IsNullOrWhiteSpace(referenceId))
        {
            throw new InvalidOperationException("XML signature reference id is empty.");
        }

        SignedXml signedXml = new(document)
        {
            SigningKey = privateKey,
        };

        Reference reference = new("#" + referenceId);
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigC14NTransform());
        signedXml.AddReference(reference);
        if (signedXml.SignedInfo is null)
        {
            throw new InvalidOperationException("XML signature information was not created.");
        }

        signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigCanonicalizationUrl;
        signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;

        KeyInfo keyInfo = new();
        keyInfo.AddClause(new KeyInfoX509Data(certificate));
        signedXml.KeyInfo = keyInfo;
        signedXml.ComputeSignature();

        XmlElement signature = signedXml.GetXml();
        XmlNode parent = elementToSign.ParentNode ?? throw new InvalidOperationException("Signed XML element has no parent node.");
        parent.AppendChild(document.ImportNode(signature, deep: true));

        return Task.FromResult(document.OuterXml);
    }

    private static XmlElement FindElementToSign(XmlDocument document, string referenceIdAttribute)
    {
        XmlNodeList elements = document.GetElementsByTagName("*");
        foreach (XmlNode node in elements)
        {
            if (node is XmlElement element && element.HasAttribute(referenceIdAttribute))
            {
                return element;
            }
        }

        throw new InvalidOperationException($"XML element with attribute '{referenceIdAttribute}' was not found.");
    }
}
