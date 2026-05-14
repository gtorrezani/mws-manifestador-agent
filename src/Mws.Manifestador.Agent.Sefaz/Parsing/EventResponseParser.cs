using System.Xml.Linq;
using Mws.Manifestador.Agent.Sefaz.Models;

namespace Mws.Manifestador.Agent.Sefaz.Parsing;

public sealed class EventResponseParser
{
    private static readonly XNamespace Nfe = "http://www.portalfiscal.inf.br/nfe";

    public EventReceptionResponse Parse(string responseXml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseXml);

        XDocument document = XDocument.Parse(responseXml, LoadOptions.PreserveWhitespace);
        XElement retEnv = document.Descendants(Nfe + "retEnvEvento").FirstOrDefault()
            ?? throw new InvalidOperationException("SEFAZ event response does not contain retEnvEvento.");
        XElement? infEvento = document.Descendants(Nfe + "retEvento").Elements(Nfe + "infEvento").FirstOrDefault();

        return new EventReceptionResponse(
            new SefazResponseMetadata(
                GetValue(retEnv, "cStat"),
                GetValue(retEnv, "xMotivo"),
                infEvento is null ? null : GetValue(infEvento, "nProt"),
                responseXml),
            infEvento is null ? null : GetValue(infEvento, "cStat"),
            infEvento is null ? null : GetValue(infEvento, "xMotivo"),
            infEvento is null ? null : GetValue(infEvento, "nProt"));
    }

    private static string? GetValue(XElement parent, string name)
    {
        return parent.Element(Nfe + name)?.Value;
    }
}
