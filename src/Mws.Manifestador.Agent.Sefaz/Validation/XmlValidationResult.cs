namespace Mws.Manifestador.Agent.Sefaz.Validation;

public sealed record XmlValidationResult(bool IsValid, IReadOnlyCollection<string> Errors);
