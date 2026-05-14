namespace Mws.Manifestador.Agent.Application.Interfaces;

public interface IAgentEnvironment
{
    string InstallationId { get; }

    string MachineName { get; }

    string Version { get; }
}
