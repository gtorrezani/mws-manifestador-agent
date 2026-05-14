namespace Mws.Manifestador.Agent.Application.Configuration;

public sealed class AgentApiOptions
{
    public const string SectionName = "AgentApi";

    public Uri BaseUrl { get; init; } = new("https://localhost");

    public string? ActivationCode { get; init; }
}
