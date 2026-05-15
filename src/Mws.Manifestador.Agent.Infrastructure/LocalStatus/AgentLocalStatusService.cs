using System.Diagnostics;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mws.Manifestador.Agent.Application.Interfaces;

namespace Mws.Manifestador.Agent.Infrastructure.LocalStatus;

[SupportedOSPlatform("windows")]
public sealed class AgentLocalStatusService
{
    public const string DefaultServiceName = "MWSManifestadorAgent";
    public const string ApplicationDirectoryName = "MWS Manifestador Agent";
    private const int MaxErrorMessageLength = 500;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly IAgentEnvironment? environment;
    private readonly string programDataDirectory;
    private readonly string serviceName;

    public AgentLocalStatusService()
        : this(null, DefaultProgramDataDirectory(), DefaultServiceName)
    {
    }

    public AgentLocalStatusService(IAgentEnvironment? environment)
        : this(environment, DefaultProgramDataDirectory(), DefaultServiceName)
    {
    }

    public AgentLocalStatusService(IAgentEnvironment? environment, string programDataDirectory, string serviceName = DefaultServiceName)
    {
        this.environment = environment;
        this.programDataDirectory = programDataDirectory;
        this.serviceName = serviceName;
    }

    public string ProgramDataDirectory => programDataDirectory;

    public string LogsDirectory => Path.Combine(programDataDirectory, "logs");

    public string StatusPath => Path.Combine(programDataDirectory, "status.json");

    public string LocalConfigurationPath => Path.Combine(programDataDirectory, "appsettings.Local.json");

    public string CredentialsPath => Path.Combine(programDataDirectory, "agent-credentials.dpapi");

    public AgentServiceState ServiceState
    {
        get
        {
            try
            {
                using ServiceController service = new(serviceName);

                return service.Status switch
                {
                    ServiceControllerStatus.Running => AgentServiceState.Running,
                    ServiceControllerStatus.Stopped => AgentServiceState.Stopped,
                    ServiceControllerStatus.StartPending => AgentServiceState.StartPending,
                    ServiceControllerStatus.StopPending => AgentServiceState.StopPending,
                    ServiceControllerStatus.Paused => AgentServiceState.Paused,
                    _ => AgentServiceState.Unknown,
                };
            }
            catch (InvalidOperationException)
            {
                return AgentServiceState.NotInstalled;
            }
        }
    }

    public AgentLocalStatusSnapshot ReadStatus()
    {
        AgentLocalStatusSnapshot stored = ReadStoredStatus();
        Uri? configuredApiBaseUrl = ReadConfiguredApiBaseUrl();
        bool activated = File.Exists(CredentialsPath);
        AgentServiceState serviceState = ServiceState;

        return stored with
        {
            ApiBaseUrl = configuredApiBaseUrl ?? stored.ApiBaseUrl,
            Activated = activated,
            ServiceStatus = serviceState.ToString(),
            Version = stored.Version ?? environment?.Version,
            InstallationId = stored.InstallationId ?? environment?.InstallationId,
        };
    }

    public async Task WriteStatusAsync(AgentLocalStatusUpdate update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);

        Directory.CreateDirectory(programDataDirectory);
        Directory.CreateDirectory(LogsDirectory);

        AgentLocalStatusSnapshot current = ReadStatus();
        AgentLocalStatusSnapshot next = new(
            SafeText(update.AgentId) ?? current.AgentId,
            SafeText(update.InstallationId) ?? current.InstallationId ?? environment?.InstallationId,
            update.ApiBaseUrl ?? current.ApiBaseUrl,
            update.Activated ?? current.Activated,
            update.LastHeartbeatAt ?? current.LastHeartbeatAt,
            update.LastPollAt ?? current.LastPollAt,
            SafeText(update.Version) ?? current.Version ?? environment?.Version,
            ServiceState.ToString(),
            SanitizeErrorMessage(update.LastErrorMessage) ?? current.LastErrorMessage);

        string temporaryPath = StatusPath + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(next, JsonOptions), cancellationToken).ConfigureAwait(false);
        File.Move(temporaryPath, StatusPath, true);
    }

    public void StartService(TimeSpan timeout)
    {
        using ServiceController service = new(serviceName);
        service.Refresh();
        if (service.Status == ServiceControllerStatus.Running)
        {
            return;
        }

        service.Start();
        service.WaitForStatus(ServiceControllerStatus.Running, timeout);
    }

    public void StopService(TimeSpan timeout)
    {
        using ServiceController service = new(serviceName);
        service.Refresh();
        if (service.Status == ServiceControllerStatus.Stopped)
        {
            return;
        }

        service.Stop();
        service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
    }

    public void RestartService(TimeSpan timeout)
    {
        StopService(timeout);
        StartService(timeout);
    }

    public void OpenConfigurator()
    {
        string configuratorPath = Path.Combine(AppContext.BaseDirectory, "Mws.Manifestador.Agent.Configurator.exe");
        Process.Start(new ProcessStartInfo(configuratorPath)
        {
            UseShellExecute = true,
        });
    }

    public void OpenLogsDirectory()
    {
        Directory.CreateDirectory(LogsDirectory);
        Process.Start(new ProcessStartInfo(LogsDirectory)
        {
            UseShellExecute = true,
        });
    }

    public string BuildBasicDiagnosticsText()
    {
        AgentLocalStatusSnapshot status = ReadStatus();

        return string.Join(
            Environment.NewLine,
            "MWS Manifestador Agent - diagnostico local",
            $"Servico: {status.ServiceStatus}",
            $"Ativado: {(status.Activated ? "Sim" : "Nao")}",
            $"API: {status.ApiBaseUrl?.ToString() ?? "Nao configurada"}",
            $"Installation ID: {status.InstallationId ?? "Nao informado"}",
            $"Agent ID: {status.AgentId ?? "Nao informado"}",
            $"Versao: {status.Version ?? "Nao informada"}",
            $"Ultimo heartbeat: {FormatDate(status.LastHeartbeatAt)}",
            $"Ultimo polling: {FormatDate(status.LastPollAt)}",
            $"Ultimo erro: {status.LastErrorMessage ?? "Nenhum"}",
            $"ProgramData: {programDataDirectory}");
    }

    public static string DefaultProgramDataDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            ApplicationDirectoryName);
    }

    private static string FormatDate(DateTimeOffset? value)
    {
        return value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", System.Globalization.CultureInfo.InvariantCulture) ?? "Nao informado";
    }

    private AgentLocalStatusSnapshot ReadStoredStatus()
    {
        if (!File.Exists(StatusPath))
        {
            return AgentLocalStatusSnapshot.Empty;
        }

        try
        {
            AgentLocalStatusSnapshot? snapshot = JsonSerializer.Deserialize<AgentLocalStatusSnapshot>(File.ReadAllText(StatusPath), JsonOptions);

            return snapshot ?? AgentLocalStatusSnapshot.Empty;
        }
        catch (JsonException)
        {
            return AgentLocalStatusSnapshot.Empty;
        }
    }

    private Uri? ReadConfiguredApiBaseUrl()
    {
        if (!File.Exists(LocalConfigurationPath))
        {
            return null;
        }

        try
        {
            JsonNode? root = JsonNode.Parse(File.ReadAllText(LocalConfigurationPath));
            string? value = root?["AgentApi"]?["BaseUrl"]?.GetValue<string>();

            return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ? uri : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? SafeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string? SanitizeErrorMessage(string? value)
    {
        string? safe = SafeText(value);
        if (safe is null)
        {
            return null;
        }

        string[] sensitiveTerms = ["secret", "password", "token", "pin", "private key", "pfx"];
        if (sensitiveTerms.Any(term => safe.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return "Mensagem sanitizada por conter dados sensiveis.";
        }

        return safe.Length <= MaxErrorMessageLength ? safe : safe[..MaxErrorMessageLength];
    }
}
