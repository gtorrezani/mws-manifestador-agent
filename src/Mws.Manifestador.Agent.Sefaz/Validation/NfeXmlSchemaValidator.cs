using System.Xml.Linq;
using System.Xml.Schema;
using Microsoft.Extensions.Options;
using Mws.Manifestador.Agent.Sefaz.Configuration;

namespace Mws.Manifestador.Agent.Sefaz.Validation;

public sealed class NfeXmlSchemaValidator
{
    private readonly SefazOptions options;

    public NfeXmlSchemaValidator(IOptions<SefazOptions> options)
    {
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public XmlValidationResult Validate(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        XElement root = document.Root ?? throw new InvalidOperationException("XML document has no root element.");
        string schemaPath = ResolveSchemaPath(root);

        XmlSchemaSet schemaSet = new();
        schemaSet.Add("http://www.portalfiscal.inf.br/nfe", schemaPath);

        List<string> errors = [];
        document.Validate(schemaSet, (_, args) => errors.Add(args.Message));

        return new XmlValidationResult(errors.Count == 0, errors);
    }

    private string ResolveSchemaPath(XElement root)
    {
        string rootName = root.Name.LocalName;
        string version = root.Attribute("versao")?.Value ?? "1.00";
        string fileName = rootName switch
        {
            "distDFeInt" => $"distDFeInt_v{version}.xsd",
            "envEvento" => $"envEvento_v{version}.xsd",
            _ => throw new InvalidOperationException($"No schema mapping configured for root '{rootName}'."),
        };

        string path = Path.Combine(options.SchemaDirectory, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Official NF-e XSD schema was not found. Configure Sefaz:SchemaDirectory with the official schema package.", path);
        }

        return path;
    }
}
