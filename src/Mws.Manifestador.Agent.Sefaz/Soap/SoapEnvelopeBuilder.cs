using System.Xml.Linq;
using Mws.Manifestador.Agent.Sefaz.Models;

namespace Mws.Manifestador.Agent.Sefaz.Soap;

public sealed class SoapEnvelopeBuilder
{
    private static readonly XNamespace Soap12 = "http://www.w3.org/2003/05/soap-envelope";

    public string Build(SefazEndpoint endpoint, string nfeXml)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(nfeXml);

        XElement payload = XElement.Parse(nfeXml, LoadOptions.PreserveWhitespace);
        XNamespace operationNamespace = endpoint.OperationNamespace;

        XDocument document = new(
            new XElement(
                Soap12 + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soap12", Soap12.NamespaceName),
                new XElement(
                    Soap12 + "Body",
                    new XElement(
                        operationNamespace + endpoint.OperationName,
                        new XElement(operationNamespace + "nfeDadosMsg", payload)))));

        return document.ToString(SaveOptions.DisableFormatting);
    }
}
