using Microsoft.Extensions.Options;
using Mws.Manifestador.Agent.Application.Configuration;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Services;

namespace Mws.Manifestador.Agent.Worker.Services;

public sealed class AgentWorker : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogStarting =
        LoggerMessage.Define(LogLevel.Information, new EventId(1000, nameof(LogStarting)), "MWS Manifestador Agent starting");

    private static readonly Action<ILogger, Exception?> LogNotActivated =
        LoggerMessage.Define(LogLevel.Warning, new EventId(1001, nameof(LogNotActivated)), "Agent is not activated; configure AgentApi:ActivationCode to activate it");

    private static readonly Action<ILogger, int, Exception?> LogPollingCompleted =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(1002, nameof(LogPollingCompleted)), "Polling cycle completed with {ProcessedCommandCount} command(s)");

    private readonly AgentActivationService activationService;
    private readonly HeartbeatService heartbeatService;
    private readonly ILogger<AgentWorker> logger;
    private readonly AgentPollingOptions options;
    private readonly PollingService pollingService;

    public AgentWorker(
        AgentActivationService activationService,
        HeartbeatService heartbeatService,
        PollingService pollingService,
        IOptions<AgentPollingOptions> options,
        ILogger<AgentWorker> logger)
    {
        this.activationService = activationService ?? throw new ArgumentNullException(nameof(activationService));
        this.heartbeatService = heartbeatService ?? throw new ArgumentNullException(nameof(heartbeatService));
        this.pollingService = pollingService ?? throw new ArgumentNullException(nameof(pollingService));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarting(logger, null);
        DateTimeOffset nextHeartbeatAt = DateTimeOffset.MinValue;

        using PeriodicTimer timer = new(TimeSpan.FromSeconds(options.IntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            AgentCredentials? credentials = await activationService.EnsureActivatedAsync(stoppingToken).ConfigureAwait(false);
            if (credentials is null)
            {
                LogNotActivated(logger, null);
                await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
                continue;
            }

            if (DateTimeOffset.UtcNow >= nextHeartbeatAt)
            {
                await heartbeatService.SendAsync(credentials, stoppingToken).ConfigureAwait(false);
                nextHeartbeatAt = DateTimeOffset.UtcNow.AddSeconds(options.HeartbeatIntervalSeconds);
            }

            int processed = await pollingService.PollAndExecuteOnceAsync(credentials, stoppingToken).ConfigureAwait(false);
            LogPollingCompleted(logger, processed, null);

            await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
        }
    }
}
