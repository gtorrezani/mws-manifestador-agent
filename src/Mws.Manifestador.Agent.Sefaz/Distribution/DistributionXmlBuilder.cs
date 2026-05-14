using System.Xml.Linq;
using Mws.Manifestador.Agent.Sefaz.Models;

namespace Mws.Manifestador.Agent.Sefaz.Distribution;

public sealed class DistributionXmlBuilder
{
    private static readonly XNamespace Nfe = "http://www.portalfiscal.inf.br/nfe";

    public string Build(DistributionQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        int selectorCount = CountSelectors(query);
        if (selectorCount != 1)
        {
            throw new InvalidOperationException("Exactly one distribution selector must be informed: LastNsu, Nsu or AccessKey.");
        }

        XElement root = new(
            Nfe + "distDFeInt",
            new XAttribute("versao", "1.01"),
            new XElement(Nfe + "tpAmb", query.Environment == Domain.Enums.SefazEnvironment.Production ? "1" : "2"),
            new XElement(Nfe + "cUFAutor", ((int)query.Uf).ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new XElement(Nfe + "CNPJ", query.Cnpj.Value));

        if (!string.IsNullOrWhiteSpace(query.LastNsu))
        {
            root.Add(new XElement(Nfe + "distNSU", new XElement(Nfe + "ultNSU", PadNsu(query.LastNsu))));
        }
        else if (!string.IsNullOrWhiteSpace(query.Nsu))
        {
            root.Add(new XElement(Nfe + "consNSU", new XElement(Nfe + "NSU", PadNsu(query.Nsu))));
        }
        else if (query.AccessKey is not null)
        {
            root.Add(new XElement(Nfe + "consChNFe", new XElement(Nfe + "chNFe", query.AccessKey.Value.Value)));
        }

        return root.ToString(SaveOptions.DisableFormatting);
    }

    private static int CountSelectors(DistributionQuery query)
    {
        int count = 0;
        if (!string.IsNullOrWhiteSpace(query.LastNsu))
        {
            count++;
        }

        if (!string.IsNullOrWhiteSpace(query.Nsu))
        {
            count++;
        }

        if (query.AccessKey is not null)
        {
            count++;
        }

        return count;
    }

    private static string PadNsu(string value)
    {
        if (value.Any(static c => !char.IsDigit(c)))
        {
            throw new InvalidOperationException("NSU must be numeric.");
        }

        return value.PadLeft(15, '0');
    }
}
