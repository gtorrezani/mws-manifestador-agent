using Mws.Manifestador.Agent.Infrastructure.LocalStatus;

namespace Mws.Manifestador.Agent.Tray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AgentLocalStatusService localStatusService;
    private readonly ContextMenuStrip menu;
    private readonly NotifyIcon notifyIcon;
    private readonly ToolStripMenuItem statusMenuItem;
    private readonly System.Windows.Forms.Timer refreshTimer;

    public TrayApplicationContext(AgentLocalStatusService localStatusService)
    {
        this.localStatusService = localStatusService ?? throw new ArgumentNullException(nameof(localStatusService));

        statusMenuItem = new ToolStripMenuItem("Carregando status...")
        {
            Enabled = false,
        };

        menu = new ContextMenuStrip();
        menu.Items.Add(statusMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(CreateMenuItem("Abrir Configurador", OpenConfigurator));
        menu.Items.Add(CreateMenuItem("Iniciar servico", StartService));
        menu.Items.Add(CreateMenuItem("Reiniciar servico", RestartService));
        menu.Items.Add(CreateMenuItem("Parar servico", StopService));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(CreateMenuItem("Abrir pasta de logs", OpenLogsDirectory));
        menu.Items.Add(CreateMenuItem("Copiar diagnostico basico", CopyDiagnostics));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(CreateMenuItem("Sair do monitor", ExitMonitor));
        menu.Opening += (_, _) => RefreshStatus();

        notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = SystemIcons.Application,
            Text = TrayResources.ApplicationName,
            Visible = true,
        };
        notifyIcon.DoubleClick += (_, _) => OpenConfigurator();

        refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 30_000,
        };
        refreshTimer.Tick += (_, _) => RefreshStatus();
        refreshTimer.Start();

        RefreshStatus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refreshTimer.Dispose();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            statusMenuItem.Dispose();
            menu.Dispose();
        }

        base.Dispose(disposing);
    }

    private static ToolStripMenuItem CreateMenuItem(string text, Action action)
    {
        return new ToolStripMenuItem(text, null, (_, _) => action());
    }

    private void RefreshStatus()
    {
        try
        {
            AgentLocalStatusSnapshot status = localStatusService.ReadStatus();
            statusMenuItem.Text = $"Servico: {status.ServiceStatus} | Ativado: {(status.Activated ? "Sim" : "Nao")}";
            notifyIcon.Text = TruncateForNotifyIcon($"MWS Agent - {status.ServiceStatus}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            statusMenuItem.Text = TrayResources.StatusUnavailable;
            notifyIcon.Text = TruncateForNotifyIcon(TrayResources.StatusUnavailableTooltip);
        }
    }

    private void OpenConfigurator()
    {
        TryAction("Abrir Configurador", localStatusService.OpenConfigurator);
    }

    private void StartService()
    {
        TryAction("Iniciar servico", () => localStatusService.StartService(TimeSpan.FromSeconds(30)));
        RefreshStatus();
    }

    private void RestartService()
    {
        TryAction("Reiniciar servico", () => localStatusService.RestartService(TimeSpan.FromSeconds(30)));
        RefreshStatus();
    }

    private void StopService()
    {
        TryAction("Parar servico", () => localStatusService.StopService(TimeSpan.FromSeconds(30)));
        RefreshStatus();
    }

    private void OpenLogsDirectory()
    {
        TryAction("Abrir pasta de logs", localStatusService.OpenLogsDirectory);
    }

    private void CopyDiagnostics()
    {
        TryAction("Copiar diagnostico basico", () =>
        {
            Clipboard.SetText(localStatusService.BuildBasicDiagnosticsText());
            MessageBox.Show(
                "Diagnostico basico copiado para a area de transferencia.",
                "MWS Agent",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        });
    }

    private void ExitMonitor()
    {
        notifyIcon.Visible = false;
        ExitThread();
    }

    private static void TryAction(string title, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(
                FriendlyMessage(exception),
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static string FriendlyMessage(Exception exception)
    {
        if (exception is UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return "A acao pode exigir permissao administrativa. Abra o Configurador como administrador ou use uma conta com permissao para controlar o servico.";
        }

        return exception.Message;
    }

    private static string TruncateForNotifyIcon(string value)
    {
        const int maxLength = 63;
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
