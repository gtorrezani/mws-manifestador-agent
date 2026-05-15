namespace Mws.Manifestador.Agent.Sefaz.Configuration;

public sealed class SefazOptions
{
    public const string SectionName = "Sefaz";

    public string SchemaDirectory { get; init; } = "schemas/nfe";

    public SchemaValidationOptions SchemaValidation { get; init; } = new();

    public DistributionOptions Distribution { get; init; } = new();

    public bool DiagnosticXmlLogging { get; init; }

    public int EventBatchLimit { get; init; } = 20;

    public bool AllowProductionDistribution { get; init; }
}

public sealed class DistributionOptions
{
    public int ConsumptionDeniedRetryAfterMinutes { get; init; } = 60;
}

public sealed class SchemaValidationOptions
{
    public bool Enabled { get; init; } = true;

    public bool Strict { get; init; }

    public string SchemasPath { get; init; } = "Schemas/NFe";

    public bool ValidateOutgoing { get; init; } = true;

    public bool ValidateIncoming { get; init; } = true;

    public bool FailOnUnknownSchema { get; init; }
}
