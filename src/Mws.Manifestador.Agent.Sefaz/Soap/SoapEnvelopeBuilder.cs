using System.Xml.Linq;
using Mws.Manifestador.Agent.Sefaz.Models;

namespace Mws.Manifestador.Agent.Sefaz.Soap;

public sealed class SoapEnvelopeBuilder
{
    private static readonly XNamespace Soap11 = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Soap12 = "http://www.w3.org/2003/05/soap-envelope";

    public string Build(SefazEndpoint endpoint, string nfeXml)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(nfeXml);

        XElement payload = XElement.Parse(nfeXml, LoadOptions.PreserveWhitespace);
        XNamespace operationNamespace = endpoint.OperationNamespace;

        XNamespace soap = endpoint.SoapVersion == SoapVersion.Soap11 ? Soap11 : Soap12;
        string soapPrefix = endpoint.SoapVersion == SoapVersion.Soap11 ? "soap" : "soap12";

        XDocument document = new(
            new XElement(
                soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + soapPrefix, soap.NamespaceName),
                new XElement(
                    soap + "Body",
                    new XElement(
                        operationNamespace + endpoint.OperationName,
                        new XElement(operationNamespace + "nfeDadosMsg", payload)))));

        return document.ToString(SaveOptions.DisableFormatting);
    }
}
