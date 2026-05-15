using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Mws.Manifestador.Agent.Application.Certificates;
using Mws.Manifestador.Agent.Application.Diagnostics;
using Mws.Manifestador.Agent.Application.Interfaces;

namespace Mws.Manifestador.Agent.Worker.Services;

public sealed class LocalDiagnosticsService : BackgroundService
{
    private static readonly string[] DiagnosticEndpoints = ["/health", "/certificates", "/diagnostics"];

    private static readonly Action<ILogger, string, Exception?> LogDiagnosticsStarted =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2000, nameof(LogDiagnosticsStarted)), "Local diagnostics listening on {ListenUrl}");

    private static readonly Action<ILogger, Exception?> LogDiagnosticsDisabled =
        LoggerMessage.Define(LogLevel.Debug, new EventId(2001, nameof(LogDiagnosticsDisabled)), "Local diagnostics endpoint is disabled");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ICertificateProvider certificateProvider;
    private readonly AgentDiagnosticsCollector diagnosticsCollector;
    private readonly ILogger<LocalDiagnosticsService> logger;
    private readonly LocalDiagnosticsOptions options;

    public LocalDiagnosticsService(
        IOptions<LocalDiagnosticsOptions> options,
        ICertificateProvider certificateProvider,
        AgentDiagnosticsCollector diagnosticsCollector,
        ILogger<LocalDiagnosticsService> logger)
    {
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.certificateProvider = certificateProvider ?? throw new ArgumentNullException(nameof(certificateProvider));
        this.diagnosticsCollector = diagnosticsCollector ?? throw new ArgumentNullException(nameof(diagnosticsCollector));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            LogDiagnosticsDisabled(logger, null);
            return;
        }

        if (!IsLoopback(options.ListenUrl))
        {
            throw new InvalidOperationException("Local diagnostics must listen only on 127.0.0.1, localhost or ::1.");
        }

        string listenUrl = NormalizePrefix(options.ListenUrl);
        using HttpListener listener = new();
        listener.Prefixes.Add(listenUrl);
        listener.Start();
        LogDiagnosticsStarted(logger, listenUrl, null);

        while (!stoppingToken.IsCancellationRequested)
        {
            HttpListenerContext context = await listener.GetContextAsync().WaitAsync(stoppingToken).ConfigureAwait(false);
            await WriteResponseAsync(context, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task WriteResponseAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        string path = context.Request.Url?.AbsolutePath ?? "/";
        string body;
        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            body = JsonSerializer.Serialize(new { status = "healthy" }, JsonOptions);
        }
        else if (path.Equals("/certificates", StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyCollection<CertificateSummary> certificates = await certificateProvider.ListAsync(cancellationToken).ConfigureAwait(false);
            body = JsonSerializer.Serialize(new { certificates }, JsonOptions);
        }
        else if (path.Equals("/diagnostics", StringComparison.OrdinalIgnoreCase))
        {
            AgentDiagnosticsSnapshot diagnostics = await diagnosticsCollector.CollectAsync(cancellationToken).ConfigureAwait(false);
            body = JsonSerializer.Serialize(new { diagnostics }, JsonOptions);
        }
        else
        {
            body = JsonSerializer.Serialize(new { status = "available", endpoints = DiagnosticEndpoints }, JsonOptions);
        }

        byte[] bytes = Encoding.UTF8.GetBytes(body);
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "application/json";
        context.Response.ContentEncoding = Encoding.UTF8;
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        context.Response.Close();
    }

    private static string NormalizePrefix(Uri value)
    {
        string prefix = value.ToString();

        return prefix.EndsWith('/') ? prefix : prefix + "/";
    }

    private static bool IsLoopback(Uri value)
    {
        return IPAddress.TryParse(value.Host, out IPAddress? address)
            ? IPAddress.IsLoopback(address)
            : value.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
    }
}
