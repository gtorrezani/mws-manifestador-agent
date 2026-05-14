using System.Net;
using System.Text;
using Microsoft.Extensions.Options;

namespace Mws.Manifestador.Agent.Worker.Services;

public sealed class LocalDiagnosticsService : BackgroundService
{
    private static readonly Action<ILogger, string, Exception?> LogDiagnosticsStarted =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2000, nameof(LogDiagnosticsStarted)), "Local diagnostics listening on {ListenUrl}");

    private static readonly Action<ILogger, Exception?> LogDiagnosticsDisabled =
        LoggerMessage.Define(LogLevel.Debug, new EventId(2001, nameof(LogDiagnosticsDisabled)), "Local diagnostics endpoint is disabled");

    private readonly ILogger<LocalDiagnosticsService> logger;
    private readonly LocalDiagnosticsOptions options;

    public LocalDiagnosticsService(
        IOptions<LocalDiagnosticsOptions> options,
        ILogger<LocalDiagnosticsService> logger)
    {
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            LogDiagnosticsDisabled(logger, null);
            return;
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

    private static async Task WriteResponseAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        string path = context.Request.Url?.AbsolutePath ?? "/";
        string body = path.Equals("/health", StringComparison.OrdinalIgnoreCase)
            ? "{\"status\":\"healthy\"}"
            : "{\"status\":\"available\",\"endpoints\":[\"/health\"]}";

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
}
