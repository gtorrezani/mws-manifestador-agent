namespace Mws.Manifestador.Agent.Sefaz.Configuration;

public sealed class SefazOptions
{
    public const string SectionName = "Sefaz";

    public string SchemaDirectory { get; init; } = "schemas/nfe";

    public bool DiagnosticXmlLogging { get; init; }

    public int EventBatchLimit { get; init; } = 20;
}
