using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Microsoft.Extensions.Options;
using Mws.Manifestador.Agent.Sefaz.Configuration;

namespace Mws.Manifestador.Agent.Sefaz.Validation;

public sealed class NfeXmlSchemaValidator
{
    private static readonly XNamespace Nfe = "http://www.portalfiscal.inf.br/nfe";
    private static readonly string[] KnownRoots =
    [
        "distDFeInt",
        "retDistDFeInt",
        "envEvento",
        "retEnvEvento",
        "resNFe",
        "resEvento",
        "NFe",
        "nfeProc",
        "procEventoNFe",
    ];

    private readonly SefazOptions options;

    public NfeXmlSchemaValidator(IOptions<SefazOptions> options)
    {
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public XmlValidationResult Validate(string xml, string? schemaName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        if (!options.SchemaValidation.Enabled)
        {
            return XmlValidationResult.Disabled();
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
        }
        catch (XmlException exception)
        {
            return XmlValidationResult.Failure(
                XmlValidationStatus.MalformedXml,
                schemaName,
                null,
                [new XmlValidationError(exception.Message, exception.LineNumber, exception.LinePosition)]);
        }

        XElement? element = FindValidatableElement(document);
        if (element is null)
        {
            return UnknownSchema(schemaName, document.Root?.Name.LocalName);
        }

        string rootElement = element.Name.LocalName;
        string? resolvedSchemaName = schemaName ?? ResolveSchemaName(element);
        if (resolvedSchemaName is null)
        {
            return UnknownSchema(schemaName, rootElement);
        }

        string schemaPath = ResolveSchemaPath(resolvedSchemaName);
        if (!File.Exists(schemaPath))
        {
            return XmlValidationResult.Failure(
                XmlValidationStatus.SchemaNotFound,
                resolvedSchemaName,
                rootElement,
                [new XmlValidationError($"Official NF-e XSD schema '{resolvedSchemaName}' was not found at '{schemaPath}'.")]);
        }

        return ValidateAgainstSchema(document, element, resolvedSchemaName, rootElement, schemaPath);
    }

    public bool ShouldFail(XmlValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsValid)
        {
            return false;
        }

        return result.Status switch
        {
            XmlValidationStatus.UnknownSchema => options.SchemaValidation.Strict || options.SchemaValidation.FailOnUnknownSchema,
            XmlValidationStatus.SchemaNotFound => options.SchemaValidation.Strict,
            _ => true,
        };
    }

    private static XElement? FindValidatableElement(XDocument document)
    {
        XElement? root = document.Root;
        if (root is null)
        {
            return null;
        }

        if (root.Name.Namespace == Nfe && KnownRoots.Contains(root.Name.LocalName, StringComparer.Ordinal))
        {
            return root;
        }

        return root.Descendants()
            .FirstOrDefault(element => element.Name.Namespace == Nfe && KnownRoots.Contains(element.Name.LocalName, StringComparer.Ordinal));
    }

    private static string? ResolveSchemaName(XElement root)
    {
        string version = root.Attribute("versao")?.Value ?? DefaultVersion(root.Name.LocalName);

        return root.Name.LocalName switch
        {
            "distDFeInt" => $"distDFeInt_v{version}.xsd",
            "retDistDFeInt" => $"retDistDFeInt_v{version}.xsd",
            "envEvento" => $"envEvento_v{version}.xsd",
            "retEnvEvento" => $"retEnvEvento_v{version}.xsd",
            "resNFe" => $"resNFe_v{version}.xsd",
            "resEvento" => $"resEvento_v{version}.xsd",
            "NFe" => $"nfe_v{version}.xsd",
            "nfeProc" => $"procNFe_v{version}.xsd",
            "procEventoNFe" => $"procEventoNFe_v{version}.xsd",
            _ => null,
        };
    }

    private static string DefaultVersion(string rootName)
    {
        return rootName switch
        {
            "distDFeInt" or "retDistDFeInt" or "resNFe" or "resEvento" => "1.01",
            "envEvento" or "retEnvEvento" or "procEventoNFe" => "1.00",
            "NFe" or "nfeProc" => "4.00",
            _ => "1.00",
        };
    }

    private string ResolveSchemaPath(string schemaName)
    {
        string configuredPath = options.SchemaDirectory;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = options.SchemaValidation.SchemasPath;
        }

        string basePath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(AppContext.BaseDirectory, configuredPath);

        return Path.Combine(basePath, schemaName);
    }

    private static XmlValidationResult ValidateAgainstSchema(
        XDocument sourceDocument,
        XElement element,
        string schemaName,
        string rootElement,
        string schemaPath)
    {
        XmlSchemaSet schemaSet = new()
        {
            XmlResolver = new XmlUrlResolver(),
        };
        schemaSet.Add(Nfe.NamespaceName, schemaPath);

        XDocument validationDocument = element.Parent is null
            ? sourceDocument
            : new XDocument(new XElement(element));

        List<XmlValidationError> errors = [];
        validationDocument.Validate(schemaSet, (_, args) =>
        {
            errors.Add(new XmlValidationError(
                args.Message,
                args.Exception?.LineNumber,
                args.Exception?.LinePosition));
        });

        return errors.Count == 0
            ? XmlValidationResult.Valid(schemaName, rootElement)
            : XmlValidationResult.Failure(XmlValidationStatus.InvalidXml, schemaName, rootElement, errors);
    }

    private static XmlValidationResult UnknownSchema(string? schemaName, string? rootElement)
    {
        return XmlValidationResult.Failure(
            XmlValidationStatus.UnknownSchema,
            schemaName,
            rootElement,
            [new XmlValidationError($"No official NF-e XSD schema mapping is configured for XML root '{rootElement ?? "unknown"}'.")]);
    }
}
