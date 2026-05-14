using System.Reflection;
using Mws.Manifestador.Agent.Application.Interfaces;

namespace Mws.Manifestador.Agent.Infrastructure.Windows;

public sealed class WindowsAgentEnvironment : IAgentEnvironment
{
    public string InstallationId => Environment.MachineName;

    public string MachineName => Environment.MachineName;

    public string Version => Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0";
}
