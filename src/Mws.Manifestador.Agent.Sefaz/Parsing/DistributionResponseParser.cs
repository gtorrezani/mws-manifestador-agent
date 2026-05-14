using System.Xml.Linq;
using Mws.Manifestador.Agent.Sefaz.Models;

namespace Mws.Manifestador.Agent.Sefaz.Parsing;

public sealed class DistributionResponseParser
{
    private static readonly XNamespace Nfe = "http://www.portalfiscal.inf.br/nfe";
    private readonly NfeDocumentDecompressor decompressor;
    private readonly FiscalDocumentParser fiscalDocumentParser;

    public DistributionResponseParser(
        NfeDocumentDecompressor decompressor,
        FiscalDocumentParser fiscalDocumentParser)
    {
        this.decompressor = decompressor;
        this.fiscalDocumentParser = fiscalDocumentParser;
    }

    public DistributionResponse Parse(string responseXml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseXml);

        XDocument document = XDocument.Parse(responseXml, LoadOptions.PreserveWhitespace);
        XElement ret = document.Descendants(Nfe + "retDistDFeInt").FirstOrDefault()
            ?? throw new InvalidOperationException("SEFAZ distribution response does not contain retDistDFeInt.");

        List<DistributedDocument> documents = [];
        foreach (XElement docZip in ret.Descendants(Nfe + "docZip"))
        {
            string schema = docZip.Attribute("schema")?.Value ?? string.Empty;
            string nsu = docZip.Attribute("NSU")?.Value ?? string.Empty;
            string xml = decompressor.DecompressDocZip(docZip.Value);
            FiscalDocumentSummary? summary = fiscalDocumentParser.TryParseSummary(xml);
            FiscalDocumentFull? full = fiscalDocumentParser.TryParseFull(xml);
            documents.Add(new DistributedDocument(schema, nsu, summary?.AccessKey ?? full?.AccessKey, xml, summary, full));
        }

        return new DistributionResponse(
            new SefazResponseMetadata(GetValue(ret, "cStat"), GetValue(ret, "xMotivo"), null, responseXml),
            GetValue(ret, "ultNSU") ?? string.Empty,
            GetValue(ret, "maxNSU") ?? string.Empty,
            documents);
    }

    private static string? GetValue(XElement parent, string name)
    {
        return parent.Element(Nfe + name)?.Value;
    }
}
