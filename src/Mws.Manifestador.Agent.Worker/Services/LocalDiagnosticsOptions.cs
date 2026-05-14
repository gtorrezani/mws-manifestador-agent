namespace Mws.Manifestador.Agent.Worker.Services;

public sealed class LocalDiagnosticsOptions
{
    public const string SectionName = "LocalDiagnostics";

    public bool Enabled { get; init; }

    public Uri ListenUrl { get; init; } = new("http://127.0.0.1:8787/");
}
