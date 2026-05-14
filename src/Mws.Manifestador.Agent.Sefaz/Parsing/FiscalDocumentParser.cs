using System.Globalization;
using System.Xml.Linq;
using Mws.Manifestador.Agent.Sefaz.Models;

namespace Mws.Manifestador.Agent.Sefaz.Parsing;

public sealed class FiscalDocumentParser
{
    private static readonly XNamespace Nfe = "http://www.portalfiscal.inf.br/nfe";

    public FiscalDocumentSummary? TryParseSummary(string xml)
    {
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement? root = document.Root;
        if (root is null || !string.Equals(root.Name.LocalName, "resNFe", StringComparison.Ordinal))
        {
            return null;
        }

        return new FiscalDocumentSummary(
            Get(root, "chNFe"),
            Get(root, "CNPJ"),
            Get(root, "xNome"),
            string.Empty,
            DateTimeOffset.Parse(Get(root, "dhEmi"), CultureInfo.InvariantCulture),
            decimal.Parse(Get(root, "vNF"), CultureInfo.InvariantCulture),
            Get(root, "cSitNFe"));
    }

    public FiscalDocumentFull? TryParseFull(string xml)
    {
        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement? infNFe = document.Descendants(Nfe + "infNFe").FirstOrDefault();
        if (infNFe is null)
        {
            return null;
        }

        XElement ide = infNFe.Element(Nfe + "ide") ?? throw new InvalidOperationException("NF-e XML does not contain ide.");
        XElement emit = infNFe.Element(Nfe + "emit") ?? throw new InvalidOperationException("NF-e XML does not contain emit.");
        XElement dest = infNFe.Element(Nfe + "dest") ?? throw new InvalidOperationException("NF-e XML does not contain dest.");
        XElement total = infNFe.Element(Nfe + "total")?.Element(Nfe + "ICMSTot") ?? throw new InvalidOperationException("NF-e XML does not contain total/ICMSTot.");

        string id = infNFe.Attribute("Id")?.Value ?? string.Empty;
        string accessKey = id.StartsWith("NFe", StringComparison.Ordinal) ? id[3..] : id;

        return new FiscalDocumentFull(
            accessKey,
            Get(emit, "CNPJ"),
            Get(emit, "xNome"),
            Get(dest, "CNPJ"),
            Get(ide, "nNF"),
            Get(ide, "serie"),
            DateTimeOffset.Parse(Get(ide, "dhEmi"), CultureInfo.InvariantCulture),
            decimal.Parse(Get(total, "vNF"), CultureInfo.InvariantCulture));
    }

    private static string Get(XElement parent, string name)
    {
        return parent.Element(Nfe + name)?.Value ?? throw new InvalidOperationException($"Required XML element '{name}' was not found.");
    }
}
