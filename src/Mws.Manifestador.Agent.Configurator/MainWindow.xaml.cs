using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mws.Manifestador.Agent.Application;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Services;
using Mws.Manifestador.Agent.Infrastructure;
using Mws.Manifestador.Agent.Infrastructure.LocalStatus;
using Mws.Manifestador.Agent.Sefaz;

namespace Mws.Manifestador.Agent.Configurator;

/// <summary>
/// Main configuration window for local Agent activation.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly AgentLocalStatusService localStatusService = new();

    public MainWindow()
    {
        InitializeComponent();
        LoadExistingApiBaseUrl();
        RefreshLocalStatus();
    }

    private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync(TestConnectionButton, TestConnectionAsync).ConfigureAwait(true);
    }

    private async void ActivateButton_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync(ActivateButton, ActivateAsync).ConfigureAwait(true);
    }

    private async void RestartServiceButton_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync(RestartServiceButton, RestartServiceAsync).ConfigureAwait(true);
    }

    private async void StartServiceButton_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync(StartServiceButton, StartServiceAsync).ConfigureAwait(true);
    }

    private async void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync(OpenLogsButton, OpenLogsAsync).ConfigureAwait(true);
    }

    private void RefreshStatusButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshLocalStatus();
    }

    private async Task RunOperationAsync(Button button, Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(button);
        ArgumentNullException.ThrowIfNull(operation);

        button.IsEnabled = false;
        try
        {
            await operation().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            SetOperationStatus("Falha: " + FriendlyMessage(exception));
        }
        finally
        {
            button.IsEnabled = true;
            RefreshLocalStatus();
        }
    }

    private async Task TestConnectionAsync()
    {
        Uri baseUri = ReadApiBaseUrl();
        using HttpClient client = new()
        {
            BaseAddress = baseUri,
            Timeout = TimeSpan.FromSeconds(15),
        };

        using HttpResponseMessage response = await client.GetAsync(new Uri("/", UriKind.Relative), CancellationToken.None).ConfigureAwait(true);
        SetOperationStatus(string.Create(CultureInfo.InvariantCulture, $"Conexao alcancou a API. HTTP {(int)response.StatusCode}."));
    }

    private async Task ActivateAsync()
    {
        Uri apiBaseUrl = ReadApiBaseUrl();
        string activationCode = ReadActivationCode();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["AgentApi:BaseUrl"] = apiBaseUrl.ToString(),
                ["AgentApi:ActivationCode"] = activationCode,
                ["Sefaz:SchemaValidation:Enabled"] = "false",
            })
            .Build();

        ServiceCollection services = new();
        services.AddLogging(static builder => builder.AddDebug());
        services
            .AddApplication()
            .AddInfrastructure(configuration)
            .AddSefazServices(configuration);

        await using ServiceProvider provider = services.BuildServiceProvider();
        AgentActivationService activationService = provider.GetRequiredService<AgentActivationService>();
        AgentCredentials? credentials = await activationService.EnsureActivatedAsync(CancellationToken.None).ConfigureAwait(true);
        WriteLocalConfiguration(apiBaseUrl);
        await localStatusService.WriteStatusAsync(
            new AgentLocalStatusUpdate
            {
                AgentId = credentials?.AgentId.ToString("D"),
                ApiBaseUrl = apiBaseUrl,
                Activated = credentials is not null,
            },
            CancellationToken.None).ConfigureAwait(true);

        ActivationCodeTextBox.Clear();
        SetOperationStatus("Agent ativado. Credenciais salvas com DPAPI. Reinicie o servico para usar a nova configuracao.");
    }

    private Task RestartServiceAsync()
    {
        localStatusService.RestartService(TimeSpan.FromSeconds(30));
        SetOperationStatus("Servico reiniciado. Aguarde o status Online na Web apos o proximo heartbeat.");

        return Task.CompletedTask;
    }

    private Task StartServiceAsync()
    {
        localStatusService.StartService(TimeSpan.FromSeconds(30));
        SetOperationStatus("Servico iniciado. Aguarde o status Online na Web apos o proximo heartbeat.");

        return Task.CompletedTask;
    }

    private Task OpenLogsAsync()
    {
        localStatusService.OpenLogsDirectory();
        SetOperationStatus("Pasta de logs aberta.");

        return Task.CompletedTask;
    }

    private Uri ReadApiBaseUrl()
    {
        string value = ApiBaseUrlTextBox.Text.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Informe uma URL HTTP ou HTTPS valida para a API.");
        }

        return uri;
    }

    private string ReadActivationCode()
    {
        string value = ActivationCodeTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Informe o codigo de ativacao gerado na Web.");
        }

        return value;
    }

    private void WriteLocalConfiguration(Uri apiBaseUrl)
    {
        Directory.CreateDirectory(localStatusService.ProgramDataDirectory);
        Directory.CreateDirectory(localStatusService.LogsDirectory);
        Directory.CreateDirectory(Path.Combine(localStatusService.ProgramDataDirectory, "temp"));

        object payload = new
        {
            AgentApi = new
            {
                BaseUrl = apiBaseUrl.ToString(),
                ActivationCode = (string?)null,
            },
            Serilog = new
            {
                WriteTo = new object[]
                {
                    new
                    {
                        Name = "File",
                        Args = new
                        {
                            Path = Path.Combine(localStatusService.LogsDirectory, "mws-agent-.log"),
                            RollingInterval = "Day",
                            RetainedFileCountLimit = 30,
                        },
                    },
                },
            },
        };

        File.WriteAllText(localStatusService.LocalConfigurationPath, JsonSerializer.Serialize(payload, JsonOptions));
    }

    private void LoadExistingApiBaseUrl()
    {
        string path = localStatusService.LocalConfigurationPath;
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.TryGetProperty("AgentApi", out JsonElement agentApi)
                && agentApi.TryGetProperty("BaseUrl", out JsonElement baseUrl)
                && baseUrl.ValueKind == JsonValueKind.String)
            {
                ApiBaseUrlTextBox.Text = baseUrl.GetString() ?? ApiBaseUrlTextBox.Text;
            }
        }
        catch (JsonException)
        {
            SetOperationStatus("Configuracao local existente nao pode ser lida. Informe a URL da API novamente.");
        }
    }

    private void RefreshLocalStatus()
    {
        AgentLocalStatusSnapshot status = localStatusService.ReadStatus();
        LocalStatusTextBlock.Text = string.Join(
            Environment.NewLine,
            $"Servico: {status.ServiceStatus}",
            $"Ativado: {(status.Activated ? "Sim" : "Nao")}",
            $"API: {status.ApiBaseUrl?.ToString() ?? "Nao configurada"}",
            $"Installation ID: {status.InstallationId ?? "Nao informado"}",
            $"Agent ID: {status.AgentId ?? "Nao informado"}",
            $"Versao: {status.Version ?? "Nao informada"}",
            $"Ultimo heartbeat: {FormatDate(status.LastHeartbeatAt)}",
            $"Ultimo polling: {FormatDate(status.LastPollAt)}",
            $"Ultimo erro: {status.LastErrorMessage ?? "Nenhum"}",
            $"Logs: {localStatusService.LogsDirectory}");
    }

    private void SetOperationStatus(string message)
    {
        OperationStatusTextBlock.Text = message;
    }

    private static string FormatDate(DateTimeOffset? value)
    {
        return value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) ?? "Nao informado";
    }

    private static string FriendlyMessage(Exception exception)
    {
        if (exception is UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return "A acao pode exigir execucao como administrador. Abra o Configurador como administrador e tente novamente.";
        }

        return exception.Message;
    }
}
