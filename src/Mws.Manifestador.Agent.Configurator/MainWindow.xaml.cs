using System.Globalization;
using System.IO;
using System.Net.Http;
using System.ServiceProcess;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mws.Manifestador.Agent.Application;
using Mws.Manifestador.Agent.Application.Services;
using Mws.Manifestador.Agent.Infrastructure;
using Mws.Manifestador.Agent.Sefaz;

namespace Mws.Manifestador.Agent.Configurator;

/// <summary>
/// Main configuration window for local Agent activation.
/// </summary>
public partial class MainWindow : Window
{
    private const string ServiceName = "MWSManifestadorAgent";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public MainWindow()
    {
        InitializeComponent();
        LoadExistingApiBaseUrl();
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
            SetStatus("Falha: " + exception.Message);
        }
        finally
        {
            button.IsEnabled = true;
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
        SetStatus(string.Create(CultureInfo.InvariantCulture, $"Conexao alcancou a API. HTTP {(int)response.StatusCode}."));
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
        await activationService.EnsureActivatedAsync(CancellationToken.None).ConfigureAwait(true);
        WriteLocalConfiguration(apiBaseUrl);
        ActivationCodeTextBox.Clear();
        SetStatus("Agent ativado. Credenciais salvas com DPAPI. Reinicie o servico para usar a nova configuracao.");
    }

    private Task RestartServiceAsync()
    {
        using ServiceController service = new(ServiceName);
        TimeSpan timeout = TimeSpan.FromSeconds(30);

        if (service.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending)
        {
            service.Stop();
            service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
        }

        service.Start();
        service.WaitForStatus(ServiceControllerStatus.Running, timeout);
        SetStatus("Servico iniciado. Aguarde o status Online na Web apos o proximo heartbeat.");

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

    private static string ProgramDataDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "MWS Manifestador Agent");
    }

    private static string LocalConfigurationPath()
    {
        return Path.Combine(ProgramDataDirectory(), "appsettings.Local.json");
    }

    private static void WriteLocalConfiguration(Uri apiBaseUrl)
    {
        Directory.CreateDirectory(ProgramDataDirectory());
        Directory.CreateDirectory(Path.Combine(ProgramDataDirectory(), "logs"));
        Directory.CreateDirectory(Path.Combine(ProgramDataDirectory(), "temp"));

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
                            Path = Path.Combine(ProgramDataDirectory(), "logs", "mws-agent-.log"),
                            RollingInterval = "Day",
                            RetainedFileCountLimit = 30,
                        },
                    },
                },
            },
        };

        File.WriteAllText(LocalConfigurationPath(), JsonSerializer.Serialize(payload, JsonOptions));
    }

    private void LoadExistingApiBaseUrl()
    {
        string path = LocalConfigurationPath();
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
            SetStatus("Configuracao local existente nao pode ser lida. Informe a URL da API novamente.");
        }
    }

    private void SetStatus(string message)
    {
        StatusTextBlock.Text = message;
    }
}
