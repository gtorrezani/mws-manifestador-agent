namespace Mws.Manifestador.Agent.Sefaz.Validation;

public enum XmlValidationStatus
{
    Valid,
    Disabled,
    MalformedXml,
    SchemaNotFound,
    UnknownSchema,
    InvalidXml,
}

public sealed record XmlValidationResult(
    bool IsValid,
    XmlValidationStatus Status,
    string? SchemaName,
    string? RootElement,
    IReadOnlyCollection<XmlValidationError> ValidationErrors)
{
    public IEnumerable<string> Errors => ValidationErrors.Select(static error => error.Message);

    public static XmlValidationResult Valid(string? schemaName, string? rootElement)
    {
        return new XmlValidationResult(true, XmlValidationStatus.Valid, schemaName, rootElement, []);
    }

    public static XmlValidationResult Disabled(string? rootElement = null)
    {
        return new XmlValidationResult(true, XmlValidationStatus.Disabled, null, rootElement, []);
    }

    public static XmlValidationResult Failure(
        XmlValidationStatus status,
        string? schemaName,
        string? rootElement,
        IReadOnlyCollection<XmlValidationError> errors)
    {
        return new XmlValidationResult(false, status, schemaName, rootElement, errors);
    }
}

public sealed record XmlValidationError(string Message, int? LineNumber = null, int? LinePosition = null);
