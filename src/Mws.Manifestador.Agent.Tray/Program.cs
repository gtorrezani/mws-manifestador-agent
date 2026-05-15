using Mws.Manifestador.Agent.Infrastructure.LocalStatus;

namespace Mws.Manifestador.Agent.Tray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        AgentLocalStatusService localStatusService = new();
        using TrayApplicationContext context = new(localStatusService);
        System.Windows.Forms.Application.Run(context);
    }
}
